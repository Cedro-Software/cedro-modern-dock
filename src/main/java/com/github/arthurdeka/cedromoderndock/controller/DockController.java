package com.github.arthurdeka.cedromoderndock.controller;

import com.github.arthurdeka.cedromoderndock.App;
import com.github.arthurdeka.cedromoderndock.application.AppServices;
import com.github.arthurdeka.cedromoderndock.application.DockTheme;
import com.github.arthurdeka.cedromoderndock.model.DockItem;
import com.github.arthurdeka.cedromoderndock.model.DockFolderItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockPositioningMode;
import com.github.arthurdeka.cedromoderndock.model.DockProgramItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockSettingsItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockWindowsModuleItemModel;
import com.github.arthurdeka.cedromoderndock.util.Logger;
import javafx.application.Platform;
import javafx.animation.PauseTransition;
import javafx.concurrent.Task;
import javafx.util.Duration;
import com.github.arthurdeka.cedromoderndock.util.NativeWindowUtils;
import com.github.arthurdeka.cedromoderndock.view.WindowPreviewPopup;
import javafx.fxml.FXML;
import javafx.fxml.FXMLLoader;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.scene.control.Button;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.layout.AnchorPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.VBox;
import javafx.geometry.Pos;
import javafx.scene.paint.Color;
import javafx.scene.shape.Circle;
import javafx.scene.shape.Rectangle;
import javafx.stage.Stage;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.function.Consumer;

import com.github.arthurdeka.cedromoderndock.util.SettingsWindowLauncher;

public class DockController {
    private static final double HOVER_SCALE = 1.3;

    @FXML
    private AnchorPane rootPane;

    @FXML
    private HBox hBoxContainer;

    private AppServices appServices;
    private Stage stage;
    // Runs native window queries off the FX thread; single daemon thread avoids unbounded thread creation.
    private final ExecutorService windowPreviewExecutor = Executors.newSingleThreadExecutor(r -> {
        Thread t = new Thread(r, "WindowPreviewFetcher");
        t.setDaemon(true);
        return t;
    });
    private WindowPreviewPopup windowPreviewPopup;
    private PauseTransition hideDebounce;
    private boolean isHoveringPopup = false;
    private Button currentHoverButton;
    private Button popupOwnerButton;
    private final ExecutorService openStateExecutor = Executors.newSingleThreadExecutor(r -> {
        Thread t = new Thread(r, "DockOpenStateWatcher");
        t.setDaemon(true);
        return t;
    });
    private final Map<String, List<Circle>> programIndicators = new HashMap<>();
    private final Rectangle dockClip = new Rectangle();
    private Consumer<Boolean> showDesktopProtectionChangeAction = enabled -> {};
    // Monotonic id to ignore stale async results from previous hover requests.
    private int hoverRequestId = 0;
    private final Runnable localizationListener = this::updateDockUI;

    // variables for the enableDrag function
    private double xOffset = 0;
    private double yOffset = 0;

    // Run when FXML is loaded
    public void handleInitialization() {
        appServices.localizationService().addListener(localizationListener);
        dockClip.widthProperty().bind(hBoxContainer.widthProperty());
        dockClip.heightProperty().bind(hBoxContainer.heightProperty());
        hBoxContainer.setClip(dockClip);

        // Popup that lists open windows for a program icon on hover.
        windowPreviewPopup = new WindowPreviewPopup();
        windowPreviewPopup.getContainer().setOnMouseEntered(e -> {
            isHoveringPopup = true;
            hideDebounce.stop();
        });
        windowPreviewPopup.getContainer().setOnMouseExited(e -> {
            isHoveringPopup = false;
            scheduleHide();
        });

        // Small delay prevents flicker when moving between icon and popup.
        hideDebounce = new PauseTransition(Duration.millis(80));
        hideDebounce.setOnFinished(e -> {
            if (shouldHidePreview()) {
                windowPreviewPopup.hide();
                popupOwnerButton = null;
            }
        });

        enableDrag();
        updateDockUI();
        startOpenStateWatcher();
    }

    // enables dock drag effect
    private void enableDrag() {
        rootPane.setOnMousePressed(event -> {
            if (!appServices.positioningService().isDynamicPositioning()) {
                return;
            }
            xOffset = event.getSceneX();
            yOffset = event.getSceneY();
        });
        rootPane.setOnMouseDragged(event -> {
            if (!appServices.positioningService().isDynamicPositioning()) {
                return;
            }
            stage.setX(event.getScreenX() - xOffset);
            stage.setY(event.getScreenY() - yOffset);
        });

        // saves the dock position on the model
        rootPane.setOnMouseReleased(event -> {
            if (!appServices.positioningService().isDynamicPositioning()) {
                appServices.positioningService().applyPosition(stage);
                return;
            }
            appServices.dockService().setDockPosition(stage.getX(), stage.getY());
            appServices.positioningService().applyPosition(stage);
        });
    }

    public void addDockItem(DockItem item) {
        appServices.dockService().addItem(item);
    }

    public void removeDockItem(int index) {
        appServices.dockService().removeItem(index);
    }

    public List<DockItem> getDockItems() {
        return appServices.dockService().getItems();
    }

    public void swapItems(int firstItemIdx, int secondItemIdx) {
        appServices.dockService().swapItems(firstItemIdx, secondItemIdx);
        updateDockUI();
    }

    /* this method updates the DockView (actual rendered Dock) style and saves the changes */
    public void updateDockUI() {
        var dock = appServices.dockService().getDock();
        hBoxContainer.getChildren().clear();
        synchronized (programIndicators) {
            programIndicators.clear();
        }
        dockClip.setArcWidth(dock.getDockBorderRounding() * 2.0);
        dockClip.setArcHeight(dock.getDockBorderRounding() * 2.0);
        hBoxContainer.setSpacing(getDockIconsSpacing());
        hBoxContainer.setStyle("-fx-background-color: rgba(" + dock.getDockColorRGB() + " " + dock.getDockTransparency() + ");" + "-fx-background-radius: " + dock.getDockBorderRounding() + ";");

        for (DockItem item : dock.getItems()) {
            Button button = createButton(item);
            if (button != null) {
                hBoxContainer.getChildren().add(button);
            }
        }

        // resize DockView window to account for DockItem additions or removing
        stage.sizeToScene();
        appServices.positioningService().applyPosition(stage);
        requestProgramIndicatorRefresh();
    }

    private Button createButton(DockItem item) {
        String buttonLabel = appServices.localizationService().dockItemLabel(item);

        if (item instanceof DockSettingsItemModel) {
            Image icon = loadDockResourceImage(item.getPath());
            ImageView imageView = createDockImageView(icon);

            Button button = new Button(buttonLabel);
            button.getStyleClass().add("dock-button");
            button.setGraphic(imageView);
            button.setOnAction(e -> appServices.itemActionService().execute(item, this::openSettingsWindow));
            return button;

        } else if (item instanceof DockWindowsModuleItemModel) {
            Image icon = loadDockResourceImage(item.getPath());
            ImageView imageView = createDockImageView(icon);

            Button button = new Button(buttonLabel);
            button.getStyleClass().add("dock-button");
            button.setGraphic(imageView);
            button.setOnAction(e -> appServices.itemActionService().execute(item, this::openSettingsWindow));
            return button;
        } else if (item instanceof DockProgramItemModel programItem) {
            // Logic for DockProgramItemModel runs on a background thread.
            Path iconPath = appServices.iconGateway().resolveProgramIcon(programItem.getExecutablePath());

            // if file does not exist
            if (iconPath == null || Files.notExists(iconPath)) {
                Logger.error("DockController - createButton - path for cached icon not found");
                return null;
            }

            Image icon = loadDockFileImage(iconPath);
            ImageView imageView = createDockImageView(icon);
            Circle runningIndicator = createRunningIndicator();
            VBox graphic = createProgramGraphic(imageView, runningIndicator);

            Button button = new Button(buttonLabel);
            button.getStyleClass().add("dock-button");
            button.setGraphic(graphic);
            synchronized (programIndicators) {
                programIndicators
                        .computeIfAbsent(programItem.getExecutablePath(), ignored -> new ArrayList<>())
                        .add(runningIndicator);
            }

            button.setOnAction(e -> appServices.itemActionService().execute(item, this::openSettingsWindow));

            // Show a window list preview when hovering this program icon.
            setupHoverPreview(button, programItem, icon);

            return button;
        } else if (item instanceof DockFolderItemModel folderItem) {
            Path iconPath = appServices.iconGateway().resolveFolderIcon(folderItem.getFolderPath());
            if (iconPath == null || Files.notExists(iconPath)) {
                appServices.iconGateway().cacheFolderIcon(folderItem.getFolderPath());
                iconPath = appServices.iconGateway().resolveFolderIcon(folderItem.getFolderPath());
            }

            Image icon;
            if (iconPath != null && Files.exists(iconPath)) {
                icon = loadDockFileImage(iconPath);
            } else {
                icon = loadDockResourceImage("/com/github/arthurdeka/cedromoderndock/icons/folder.png");
            }

            ImageView imageView = createDockImageView(icon);

            Button button = new Button(buttonLabel);
            button.getStyleClass().add("dock-button");
            button.setGraphic(imageView);
            button.setOnAction(e -> appServices.itemActionService().execute(item, this::openSettingsWindow));
            return button;

        } else {
            return null;
        }
    }

    private VBox createProgramGraphic(ImageView imageView, Circle runningIndicator) {
        VBox graphic = new VBox(4, imageView, runningIndicator);
        graphic.setAlignment(Pos.CENTER);
        return graphic;
    }

    private Image loadDockResourceImage(String resourcePath) {
        return new Image(
                App.class.getResource(resourcePath).toExternalForm(),
                getRequestedIconSize(),
                getRequestedIconSize(),
                true,
                true,
                false
        );
    }

    private Image loadDockFileImage(Path iconPath) {
        return new Image(
                iconPath.toUri().toString(),
                getRequestedIconSize(),
                getRequestedIconSize(),
                true,
                true,
                false
        );
    }

    private ImageView createDockImageView(Image icon) {
        ImageView imageView = new ImageView(icon);
        imageView.setFitWidth(appServices.appearanceService().getIconsSize());
        imageView.setFitHeight(appServices.appearanceService().getIconsSize());
        imageView.setPreserveRatio(true);
        imageView.setSmooth(true);
        return imageView;
    }

    private int getRequestedIconSize() {
        return Math.max(
                appServices.appearanceService().getIconsSize(),
                (int) Math.ceil(appServices.appearanceService().getIconsSize() * HOVER_SCALE)
        );
    }

    private Circle createRunningIndicator() {
        Circle runningIndicator = new Circle(1.5);
        runningIndicator.setFill(Color.WHITE);
        runningIndicator.setManaged(true);
        runningIndicator.setOpacity(0);
        runningIndicator.setMouseTransparent(true);
        return runningIndicator;
    }

    private void startOpenStateWatcher() {
        openStateExecutor.execute(() -> {
            while (!Thread.currentThread().isInterrupted()) {
                try {
                    refreshProgramIndicatorsInBackground();
                    Thread.sleep(1200);
                } catch (InterruptedException e) {
                    Thread.currentThread().interrupt();
                } catch (Exception e) {
                    Logger.error("Failed to refresh running program indicators: " + e.getMessage());
                }
            }
        });
    }

    private void requestProgramIndicatorRefresh() {
        openStateExecutor.execute(this::refreshProgramIndicatorsInBackground);
    }

    private void refreshProgramIndicatorsInBackground() {
        Map<String, List<Circle>> indicatorSnapshot = snapshotProgramIndicators();
        if (indicatorSnapshot.isEmpty()) {
            return;
        }

        Map<String, Boolean> openStateByExecutable = new HashMap<>();
        for (String executablePath : indicatorSnapshot.keySet()) {
            openStateByExecutable.put(executablePath, appServices.windowPreviewService().hasOpenWindows(executablePath));
        }

        Platform.runLater(() -> applyProgramIndicatorState(indicatorSnapshot, openStateByExecutable));
    }

    private Map<String, List<Circle>> snapshotProgramIndicators() {
        synchronized (programIndicators) {
            Map<String, List<Circle>> snapshot = new HashMap<>();
            for (Map.Entry<String, List<Circle>> entry : programIndicators.entrySet()) {
                snapshot.put(entry.getKey(), new ArrayList<>(entry.getValue()));
            }
            return snapshot;
        }
    }

    private void applyProgramIndicatorState(Map<String, List<Circle>> indicatorSnapshot, Map<String, Boolean> openStateByExecutable) {
        for (Map.Entry<String, Boolean> entry : openStateByExecutable.entrySet()) {
            List<Circle> indicators = indicatorSnapshot.getOrDefault(entry.getKey(), List.of());
            boolean isOpen = entry.getValue();
            for (Circle indicator : indicators) {
                indicator.setOpacity(isOpen ? 1 : 0);
            }
        }
    }

    private void setupHoverPreview(Button button, DockProgramItemModel item, Image icon) {
        // Track hover state for the icon and popup to avoid flicker.
        button.setOnMouseEntered(e -> {
            currentHoverButton = button;
            hideDebounce.stop();
            // If another icon owns the popup, close it before showing new content.
            if (windowPreviewPopup.isShowing() && popupOwnerButton != button) {
                windowPreviewPopup.hide();
                popupOwnerButton = null;
            }
            showWindowPreview(button, item, icon);
        });

        button.setOnMouseExited(e -> {
            if (currentHoverButton == button) {
                currentHoverButton = null;
            }
            // Hide with a short debounce to allow moving into the popup.
            scheduleHide();
        });
    }

    private void showWindowPreview(Button button, DockProgramItemModel item, Image icon) {
        int requestId = ++hoverRequestId;
        // Query native windows on a background thread.
        Task<List<NativeWindowUtils.WindowInfo>> task = new Task<>() {
            @Override
            protected List<NativeWindowUtils.WindowInfo> call() throws Exception {
                return appServices.windowPreviewService().loadPreview(item);
            }
        };

        task.setOnSucceeded(e -> {
            // Ignore results from older hover requests.
            if (requestId != hoverRequestId) {
                return;
            }
            List<NativeWindowUtils.WindowInfo> windows = task.getValue();
            // If the mouse left the icon, do nothing.
            if (currentHoverButton != button || !button.isHover()) {
                return;
            }
            // Only show popup when there is at least one window.
            if (!windows.isEmpty()) {
                DockTheme dockTheme = appServices.appearanceService().getDockTheme();
                windowPreviewPopup.updateContent(
                        windows,
                        icon,
                        item.getLabel(),
                        dockTheme,
                        appServices.windowPreviewService()::activate
                );
                windowPreviewPopup.showAbove(button, hBoxContainer);
                popupOwnerButton = button;

            } else if (windowPreviewPopup.isShowing() && popupOwnerButton == button) {
                windowPreviewPopup.hide();
                popupOwnerButton = null;
            }
        });

        task.setOnFailed(e -> {
            Logger.error("Failed to fetch windows for " + item.getLabel() + ": " + task.getException().getMessage());
        });

        windowPreviewExecutor.execute(task);
    }

    private void scheduleHide() {
        hideDebounce.stop();
        hideDebounce.playFromStart();
    }

    private boolean shouldHidePreview() {
        if (isHoveringPopup) {
            return false;
        }
        if (currentHoverButton == null) {
            return true;
        }
        return !currentHoverButton.isHover();
    }

    private void openSettingsWindow() {
        SettingsWindowLauncher.open(
                appServices,
                this::updateDockUI,
                this::handlePositioningModeChange,
                showDesktopProtectionChangeAction
        );
    }

    public void setDockIconsSize(int iconsSize) {
        appServices.appearanceService().setIconsSize(iconsSize);
        updateDockUI();
    }

    public int getDockIconsSize() {
        return appServices.appearanceService().getIconsSize();
    }

    public void setDockIconsSpacing(int spacingValue) {
        appServices.appearanceService().setSpacingBetweenIcons(spacingValue);
        updateDockUI();
    }

    public int getDockIconsSpacing() {
        return appServices.appearanceService().getSpacingBetweenIcons();
    }

    public int getDockTransparency() {
        return appServices.appearanceService().getDockTransparencyPercentage();
    }

    public void setDockTransparency(int value) {
        appServices.appearanceService().setDockTransparencyPercentage(value);
        updateDockUI();
    }

    public void setDockBorderRounding(int value) {
        appServices.appearanceService().setDockBorderRounding(value);
        updateDockUI();

    }

    public int getDockBorderRounding() {
        return appServices.appearanceService().getDockBorderRounding();
    }

    public void setStage(Stage stage) {
        this.stage = stage;
        this.stage.setOnHidden(event -> {
            appServices.localizationService().removeListener(localizationListener);
            windowPreviewExecutor.shutdownNow();
            openStateExecutor.shutdownNow();
        });
    }

    public String getDockColorRGB() {
        return appServices.appearanceService().getDockColorRGB();
    }

    public void setDockColorRGB(String value) {
        appServices.appearanceService().setDockColorRGB(value);
        updateDockUI();
    }

    public void saveChanges() {
        appServices.dockService().saveChanges();
    }

    public void setAppServices(AppServices appServices) {
        this.appServices = appServices;
    }

    public void setShowDesktopProtectionChangeAction(Consumer<Boolean> showDesktopProtectionChangeAction) {
        this.showDesktopProtectionChangeAction = showDesktopProtectionChangeAction;
    }

    private void handlePositioningModeChange(DockPositioningMode positioningMode) {
        DockPositioningMode currentMode = appServices.positioningService().getPositioningMode();
        if (currentMode == DockPositioningMode.STATIC && positioningMode == DockPositioningMode.DYNAMIC) {
            appServices.dockService().setDockPosition(stage.getX(), stage.getY());
        }
        appServices.positioningService().setPositioningMode(positioningMode);
    }
}
