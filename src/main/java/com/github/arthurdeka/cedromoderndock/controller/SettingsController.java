package com.github.arthurdeka.cedromoderndock.controller;

import com.github.arthurdeka.cedromoderndock.App;
import com.github.arthurdeka.cedromoderndock.application.AppServices;
import com.github.arthurdeka.cedromoderndock.application.ProgramSelectionResolver;
import com.github.arthurdeka.cedromoderndock.application.SupportedLanguage;
import com.github.arthurdeka.cedromoderndock.model.DockHorizontalAnchor;
import com.github.arthurdeka.cedromoderndock.model.DockItem;
import com.github.arthurdeka.cedromoderndock.model.DockFolderItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockPositioningMode;
import com.github.arthurdeka.cedromoderndock.model.DockProgramItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockSettingsItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockVerticalAnchor;
import com.github.arthurdeka.cedromoderndock.util.ColorManipulation;
import com.github.arthurdeka.cedromoderndock.util.Logger;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import javafx.event.ActionEvent;
import javafx.fxml.FXML;
import javafx.fxml.FXMLLoader;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.scene.control.Button;
import javafx.scene.control.CheckBox;
import javafx.scene.control.ChoiceBox;
import javafx.scene.control.ColorPicker;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.control.RadioButton;
import javafx.scene.control.Slider;
import javafx.scene.control.Tab;
import javafx.scene.layout.VBox;
import javafx.scene.paint.Color;
import javafx.stage.DirectoryChooser;
import javafx.stage.FileChooser;
import javafx.stage.Stage;
import javafx.stage.WindowEvent;
import javafx.util.StringConverter;

import java.io.File;
import java.nio.file.Path;
import java.util.Collections;
import java.util.List;
import java.util.function.Consumer;

import static com.github.arthurdeka.cedromoderndock.util.UIUtils.setStageIcon;

public class SettingsController {
    private static final double LIST_VIEW_CELL_HEIGHT = 40;
    private static final double LIST_VIEW_MAX_HEIGHT = 200;

    @FXML
    private Label mainTitleLabel;
    @FXML
    private Label mainSubtitleLabel;
    @FXML
    private Label languageLabel;
    @FXML
    private ChoiceBox<SupportedLanguage> languageChoiceBox;
    @FXML
    private Tab iconsTab;
    @FXML
    private Tab iconsCustomizationTab;
    @FXML
    private Tab dockCustomizationTab;
    @FXML
    private Tab dockPositioningTab;
    @FXML
    private Tab generalTab;

    @FXML
    private ListView<String> listView;
    @FXML
    private Label dockItemsTitleLabel;
    @FXML
    private Label dockItemsHelperLabel;
    @FXML
    private Label actionsTitleLabel;
    @FXML
    private Label actionsHelperLabel;
    @FXML
    private Button addProgramButton;
    @FXML
    private Button addFolderButton;
    @FXML
    private Button addWindowsModuleButton;
    @FXML
    private Button removeProgramButton;
    @FXML
    private Button moveItemUpButton;
    @FXML
    private Button moveItemDownButton;

    @FXML
    private Slider iconSizeSlider;
    @FXML
    private Slider spacingBetweenIconsSlider;
    @FXML
    private Label iconsSizeTitleLabel;
    @FXML
    private Label iconsSizeHelperLabel;
    @FXML
    private Label spacingTitleLabel;
    @FXML
    private Label spacingHelperLabel;

    @FXML
    private Slider dockTransparencySlider;
    @FXML
    private Slider dockBorderRoundingSlider;
    @FXML
    private ColorPicker dockColorPicker;
    @FXML
    private Label transparencyTitleLabel;
    @FXML
    private Label transparencyHelperLabel;
    @FXML
    private Label roundingTitleLabel;
    @FXML
    private Label roundingHelperLabel;
    @FXML
    private Label backgroundColorTitleLabel;
    @FXML
    private Label backgroundColorHelperLabel;

    @FXML
    private RadioButton staticPositioningRadio;
    @FXML
    private RadioButton dynamicPositioningRadio;
    @FXML
    private ChoiceBox<DockVerticalAnchor> verticalPositionChoiceBox;
    @FXML
    private ChoiceBox<DockHorizontalAnchor> horizontalPositionChoiceBox;
    @FXML
    private Slider topSpacingSlider;
    @FXML
    private Slider leftSpacingSlider;
    @FXML
    private Slider rightSpacingSlider;
    @FXML
    private Slider bottomSpacingSlider;
    @FXML
    private VBox staticPositioningPane;
    @FXML
    private VBox dynamicPositioningPane;
    @FXML
    private Label positioningModeTitleLabel;
    @FXML
    private Label positioningModeHelperLabel;
    @FXML
    private Label alignmentTitleLabel;
    @FXML
    private Label verticalLabel;
    @FXML
    private Label horizontalLabel;
    @FXML
    private Label screenSpacingTitleLabel;
    @FXML
    private Label topSpacingLabel;
    @FXML
    private Label leftSpacingLabel;
    @FXML
    private Label rightSpacingLabel;
    @FXML
    private Label bottomSpacingLabel;
    @FXML
    private Label dynamicPositioningTitleLabel;
    @FXML
    private Label dynamicPositioningHelperLabel;

    @FXML
    private Label versionLabel;
    @FXML
    private Label repositoryLabel;
    @FXML
    private Label contactLabel;
    @FXML
    private Label openSourceLabel;
    @FXML
    private Button acknowledgementsButton;
    @FXML
    private Label showDesktopProtectionTitleLabel;
    @FXML
    private Label showDesktopProtectionHelperLabel;
    @FXML
    private CheckBox showDesktopProtectionCheckBox;

    private final ObservableList<String> listItems = FXCollections.observableArrayList();
    private final Runnable localizationListener = this::handleLocalizationChanged;
    private boolean suppressionEnabled;
    private boolean localizationRegistered;
    private boolean cleanupRegistered;

    private AppServices appServices;
    private Runnable dockRefreshAction = () -> {};
    private Consumer<DockPositioningMode> positioningModeChangeAction = positioningMode -> {};
    private Consumer<Boolean> showDesktopProtectionChangeAction = enabled -> {};

    public void initialize() {
        Logger.info("[Initializing] SettingsController");

        javafx.scene.control.ToggleGroup positioningModeGroup = new javafx.scene.control.ToggleGroup();
        staticPositioningRadio.setToggleGroup(positioningModeGroup);
        dynamicPositioningRadio.setToggleGroup(positioningModeGroup);
        listView.setFixedCellSize(LIST_VIEW_CELL_HEIGHT);
        listView.sceneProperty().addListener((observableValue, oldScene, newScene) -> {
            if (newScene == null) {
                return;
            }

            newScene.windowProperty().addListener((windowObservable, oldWindow, newWindow) -> {
                if (newWindow == null) {
                    return;
                }
                updateWindowTitle();
                if (!cleanupRegistered) {
                    cleanupRegistered = true;
                    newWindow.addEventHandler(WindowEvent.WINDOW_HIDDEN, event -> unregisterLocalizationListener());
                }
            });
        });
    }

    public void handleInitialization() {
        registerLocalization();
        configureLocalizationControls();

        addDockItemsToListView(appServices.dockService().getItems());
        listView.getSelectionModel().selectedItemProperty().addListener((observableValue, oldValue, newValue) -> handleListViewItemSelection());

        iconSizeSlider.setValue(appServices.appearanceService().getIconsSize());
        iconSizeSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> handleSetIconSizeSlider((int) iconSizeSlider.getValue()));

        spacingBetweenIconsSlider.setValue(appServices.appearanceService().getSpacingBetweenIcons());
        spacingBetweenIconsSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> handleSetIconsSpacingSlider((int) spacingBetweenIconsSlider.getValue()));

        dockTransparencySlider.setValue(appServices.appearanceService().getDockTransparencyPercentage());
        dockTransparencySlider.valueProperty().addListener((observableValue, oldValue, newValue) -> handleSetDockTransparencySlider((int) dockTransparencySlider.getValue()));

        dockBorderRoundingSlider.setValue(appServices.appearanceService().getDockBorderRounding());
        dockBorderRoundingSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> handleSetDockBorderRoundingSlider((int) dockBorderRoundingSlider.getValue()));

        Color rgbaColor = ColorManipulation.fromRGBtoRGBA(appServices.appearanceService().getDockColorRGB());
        dockColorPicker.setValue(rgbaColor);

        initializePositioningControls();
        initializeGeneralControls();
        applyLocalizedTexts();
    }

    private void initializeGeneralControls() {
        showDesktopProtectionCheckBox.setSelected(appServices.dockService().getDock().isShowDesktopProtectionEnabled());
        showDesktopProtectionCheckBox.selectedProperty().addListener((observableValue, oldValue, newValue) -> {
            if (suppressionEnabled) {
                return;
            }
            showDesktopProtectionChangeAction.accept(Boolean.TRUE.equals(newValue));
        });
    }

    private void registerLocalization() {
        if (!localizationRegistered) {
            appServices.localizationService().addListener(localizationListener);
            localizationRegistered = true;
        }
    }

    private void unregisterLocalizationListener() {
        if (appServices != null && localizationRegistered) {
            appServices.localizationService().removeListener(localizationListener);
            localizationRegistered = false;
        }
    }

    private void configureLocalizationControls() {
        languageChoiceBox.setConverter(new StringConverter<>() {
            @Override
            public String toString(SupportedLanguage language) {
                return language == null ? "" : appServices.localizationService().languageDisplayName(language);
            }

            @Override
            public SupportedLanguage fromString(String string) {
                return appServices.localizationService().getCurrentLanguage();
            }
        });

        verticalPositionChoiceBox.setConverter(new StringConverter<>() {
            @Override
            public String toString(DockVerticalAnchor anchor) {
                return anchor == null ? "" : verticalAnchorText(anchor);
            }

            @Override
            public DockVerticalAnchor fromString(String string) {
                return DockVerticalAnchor.TOP;
            }
        });

        horizontalPositionChoiceBox.setConverter(new StringConverter<>() {
            @Override
            public String toString(DockHorizontalAnchor anchor) {
                return anchor == null ? "" : horizontalAnchorText(anchor);
            }

            @Override
            public DockHorizontalAnchor fromString(String string) {
                return DockHorizontalAnchor.LEFT;
            }
        });

        languageChoiceBox.getSelectionModel().selectedItemProperty().addListener((observableValue, oldValue, newValue) -> {
            if (suppressionEnabled || newValue == null) {
                return;
            }

            appServices.localizationService().setLanguage(newValue);
            dockRefreshAction.run();
        });
    }

    private void initializePositioningControls() {
        DockPositioningMode positioningMode = appServices.positioningService().getPositioningMode();
        if (positioningMode == DockPositioningMode.DYNAMIC) {
            dynamicPositioningRadio.setSelected(true);
        } else {
            staticPositioningRadio.setSelected(true);
        }

        verticalPositionChoiceBox.setValue(appServices.positioningService().getVerticalAnchor());
        horizontalPositionChoiceBox.setValue(appServices.positioningService().getHorizontalAnchor());
        topSpacingSlider.setValue(appServices.positioningService().getTopSpacing());
        leftSpacingSlider.setValue(appServices.positioningService().getLeftSpacing());
        rightSpacingSlider.setValue(appServices.positioningService().getRightSpacing());
        bottomSpacingSlider.setValue(appServices.positioningService().getBottomSpacing());

        verticalPositionChoiceBox.getSelectionModel().selectedItemProperty().addListener((observableValue, oldValue, newValue) -> {
            if (suppressionEnabled || newValue == null) {
                return;
            }
            appServices.positioningService().setVerticalAnchor(newValue);
            dockRefreshAction.run();
        });

        horizontalPositionChoiceBox.getSelectionModel().selectedItemProperty().addListener((observableValue, oldValue, newValue) -> {
            if (suppressionEnabled || newValue == null) {
                return;
            }
            appServices.positioningService().setHorizontalAnchor(newValue);
            dockRefreshAction.run();
        });

        topSpacingSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> {
            appServices.positioningService().setTopSpacing((int) Math.round(newValue.doubleValue()));
            dockRefreshAction.run();
        });

        leftSpacingSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> {
            appServices.positioningService().setLeftSpacing((int) Math.round(newValue.doubleValue()));
            dockRefreshAction.run();
        });

        rightSpacingSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> {
            appServices.positioningService().setRightSpacing((int) Math.round(newValue.doubleValue()));
            dockRefreshAction.run();
        });

        bottomSpacingSlider.valueProperty().addListener((observableValue, oldValue, newValue) -> {
            appServices.positioningService().setBottomSpacing((int) Math.round(newValue.doubleValue()));
            dockRefreshAction.run();
        });

        updatePositioningModeUI();
    }

    @FXML
    private void handlePositioningModeChange() {
        DockPositioningMode positioningMode = staticPositioningRadio.isSelected()
                ? DockPositioningMode.STATIC
                : DockPositioningMode.DYNAMIC;
        positioningModeChangeAction.accept(positioningMode);
        updatePositioningModeUI();
        dockRefreshAction.run();
    }

    private void updatePositioningModeUI() {
        boolean isStaticMode = staticPositioningRadio.isSelected();
        staticPositioningPane.setVisible(isStaticMode);
        staticPositioningPane.setManaged(isStaticMode);
        dynamicPositioningPane.setVisible(!isStaticMode);
        dynamicPositioningPane.setManaged(!isStaticMode);
    }

    private void handleListViewItemSelection() {
        int selectedIdx = listView.getSelectionModel().getSelectedIndex();
        if (selectedIdx < 0) {
            removeProgramButton.setDisable(true);
            moveItemUpButton.setDisable(true);
            moveItemDownButton.setDisable(true);
            return;
        }

        DockItem item = appServices.dockService().getItems().get(selectedIdx);
        removeProgramButton.setDisable(item instanceof DockSettingsItemModel);
        moveItemUpButton.setDisable(selectedIdx == 0);
        moveItemDownButton.setDisable(selectedIdx == listItems.size() - 1);
    }

    private void addDockItemsToListView(List<DockItem> dockItems) {
        listItems.clear();

        for (DockItem item : dockItems) {
            String label = appServices.localizationService().dockItemLabel(item);
            listItems.add(label);
            Logger.info("[Initializing][listView] Adding item to ListView: " + label);
        }

        listView.setItems(listItems);
        updateListViewHeight();
    }

    private void updateListViewHeight() {
        int visibleRows = Math.max(1, listItems.size());
        double contentHeight = visibleRows * listView.getFixedCellSize() + 2;
        double boundedHeight = Math.min(contentHeight, LIST_VIEW_MAX_HEIGHT);

        listView.setMinHeight(boundedHeight);
        listView.setPrefHeight(boundedHeight);
        listView.setMaxHeight(LIST_VIEW_MAX_HEIGHT);
    }

    @FXML
    private void openAddWindowsModuleWindow() {
        try {
            Stage currentStage = (Stage) listView.getScene().getWindow();

            FXMLLoader loader = new FXMLLoader(App.class.getResource("fxml/AddWindowsModulesModalView.fxml"));
            Parent root = loader.load();

            AddWindowsModulesModalController addWindowsModulesModalController = loader.getController();
            addWindowsModulesModalController.setAppServices(appServices);
            addWindowsModulesModalController.setDockRefreshAction(dockRefreshAction);
            addWindowsModulesModalController.setPositioningModeChangeAction(positioningModeChangeAction);
            addWindowsModulesModalController.handleInitialization();

            Stage stage = new Stage();
            stage.setTitle(text("windowsModule.modal.title"));
            setStageIcon(stage);
            stage.setScene(new Scene(root));
            stage.show();

            currentStage.close();
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    @FXML
    private void handleAddProgram() {
        FileChooser fileChooser = new FileChooser();
        fileChooser.setTitle(text("dialog.fileChooser.executableTitle"));
        fileChooser.getExtensionFilters().add(
                new FileChooser.ExtensionFilter(text("dialog.fileChooser.executableFilter"), "*.exe")
        );

        File file = fileChooser.showOpenDialog(null);
        if (file != null) {
            ProgramSelectionResolver.ResolvedProgramSelection selection =
                    ProgramSelectionResolver.resolve(Path.of(file.getAbsolutePath()));
            String selectedExePath = selection.executablePath();
            String selectedExeName = selection.label();

            appServices.iconGateway().cacheProgramIcon(selectedExePath);
            DockItem newItem = new DockProgramItemModel(selectedExeName, selectedExePath);
            appServices.dockService().addItem(newItem);
            Logger.info("[listView] Program added: " + selectedExeName);
        }

        addDockItemsToListView(appServices.dockService().getItems());
        dockRefreshAction.run();
    }

    @FXML
    private void handleAddFolder() {
        DirectoryChooser directoryChooser = new DirectoryChooser();
        directoryChooser.setTitle(text("dialog.directoryChooser.title"));

        Stage owner = (Stage) listView.getScene().getWindow();
        File selectedFolder = directoryChooser.showDialog(owner);
        if (selectedFolder == null || !selectedFolder.isDirectory()) {
            return;
        }

        String folderPath = selectedFolder.getAbsolutePath();
        String folderName = selectedFolder.getName();
        String label = (folderName == null || folderName.isBlank()) ? folderPath : folderName;

        appServices.iconGateway().cacheFolderIcon(folderPath);
        DockItem newItem = new DockFolderItemModel(label, folderPath);
        appServices.dockService().addItem(newItem);
        Logger.info("[listView] Folder added: " + label);

        addDockItemsToListView(appServices.dockService().getItems());
        dockRefreshAction.run();
    }

    @FXML
    private void handleRemoveProgram() {
        int selectedIdx = listView.getSelectionModel().getSelectedIndex();
        Logger.info("[listView] Removing item on index: " + selectedIdx);

        appServices.dockService().removeItem(selectedIdx);
        addDockItemsToListView(appServices.dockService().getItems());
        dockRefreshAction.run();
    }

    @FXML
    private void handleMoveItem(ActionEvent event) {
        int selectedIdx = listView.getSelectionModel().getSelectedIndex();
        if (selectedIdx < 0) {
            return;
        }

        int newSelectedIdx = selectedIdx;
        if (event.getSource() == moveItemUpButton) {
            if (selectedIdx == 0) {
                return;
            }
            Logger.info("[listView] moving item up");
            Collections.swap(listItems, selectedIdx, selectedIdx - 1);
            appServices.dockService().swapItems(selectedIdx, selectedIdx - 1);
            newSelectedIdx = selectedIdx - 1;
        } else {
            if (selectedIdx >= listItems.size() - 1) {
                return;
            }
            Logger.info("[listView] moving item down");
            Collections.swap(listItems, selectedIdx, selectedIdx + 1);
            appServices.dockService().swapItems(selectedIdx, selectedIdx + 1);
            newSelectedIdx = selectedIdx + 1;
        }

        addDockItemsToListView(appServices.dockService().getItems());
        listView.getSelectionModel().select(newSelectedIdx);
        handleListViewItemSelection();
        dockRefreshAction.run();
    }

    private void handleSetIconSizeSlider(int value) {
        appServices.appearanceService().setIconsSize(value);
        dockRefreshAction.run();
    }

    private void handleSetIconsSpacingSlider(int value) {
        appServices.appearanceService().setSpacingBetweenIcons(value);
        dockRefreshAction.run();
    }

    private void handleSetDockTransparencySlider(int value) {
        appServices.appearanceService().setDockTransparencyPercentage(value);
        dockRefreshAction.run();
    }

    private void handleSetDockBorderRoundingSlider(int value) {
        appServices.appearanceService().setDockBorderRounding(value);
        dockRefreshAction.run();
    }

    @FXML
    private void handleSetDockColor() {
        String rgbaColor = String.valueOf(dockColorPicker.getValue());
        String rgbColor = ColorManipulation.fromRGBAtoRGB(rgbaColor);

        appServices.appearanceService().setDockColorRGB(rgbColor);
        dockRefreshAction.run();
    }

    public void setAppServices(AppServices appServices) {
        this.appServices = appServices;
    }

    public void setDockRefreshAction(Runnable dockRefreshAction) {
        this.dockRefreshAction = dockRefreshAction;
    }

    public void setPositioningModeChangeAction(Consumer<DockPositioningMode> positioningModeChangeAction) {
        this.positioningModeChangeAction = positioningModeChangeAction;
    }

    public void setShowDesktopProtectionChangeAction(Consumer<Boolean> showDesktopProtectionChangeAction) {
        this.showDesktopProtectionChangeAction = showDesktopProtectionChangeAction;
    }

    @FXML
    private void openAknowledgementsWindow() {
        try {
            FXMLLoader loader = new FXMLLoader(App.class.getResource("fxml/AcknowledgementsModalView.fxml"));
            Parent root = loader.load();

            AcknowledgementsModalController controller = loader.getController();
            controller.setAppServices(appServices);
            controller.handleInitialization();

            Stage stage = new Stage();
            stage.setTitle(text("acknowledgements.window.title"));
            setStageIcon(stage);
            stage.setScene(new Scene(root));
            stage.show();
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    private void handleLocalizationChanged() {
        applyLocalizedTexts();
        addDockItemsToListView(appServices.dockService().getItems());
        handleListViewItemSelection();
    }

    private void applyLocalizedTexts() {
        if (appServices == null) {
            return;
        }

        suppressionEnabled = true;
        languageChoiceBox.setItems(FXCollections.observableArrayList(SupportedLanguage.values()));
        languageChoiceBox.setValue(appServices.localizationService().getCurrentLanguage());
        verticalPositionChoiceBox.setItems(FXCollections.observableArrayList(DockVerticalAnchor.values()));
        verticalPositionChoiceBox.setValue(appServices.positioningService().getVerticalAnchor());
        horizontalPositionChoiceBox.setItems(FXCollections.observableArrayList(DockHorizontalAnchor.values()));
        horizontalPositionChoiceBox.setValue(appServices.positioningService().getHorizontalAnchor());
        suppressionEnabled = false;

        mainTitleLabel.setText(text("settings.page.title"));
        mainSubtitleLabel.setText(text("settings.page.subtitle"));
        languageLabel.setText(text("settings.language.label"));

        iconsTab.setText(text("settings.tab.icons"));
        iconsCustomizationTab.setText(text("settings.tab.iconsCustomization"));
        dockCustomizationTab.setText(text("settings.tab.dockCustomization"));
        dockPositioningTab.setText(text("settings.tab.dockPositioning"));
        generalTab.setText(text("settings.tab.general"));

        dockItemsTitleLabel.setText(text("settings.icons.items.title"));
        dockItemsHelperLabel.setText(text("settings.icons.items.helper"));
        actionsTitleLabel.setText(text("settings.icons.actions.title"));
        actionsHelperLabel.setText(text("settings.icons.actions.helper"));
        moveItemUpButton.setText(text("settings.icons.moveUp"));
        moveItemDownButton.setText(text("settings.icons.moveDown"));
        addProgramButton.setText(text("settings.icons.addProgram"));
        addFolderButton.setText(text("settings.icons.addFolder"));
        addWindowsModuleButton.setText(text("settings.icons.addWindowsModule"));
        removeProgramButton.setText(text("settings.icons.removeSelected"));

        iconsSizeTitleLabel.setText(text("settings.iconsCustomization.size.title"));
        iconsSizeHelperLabel.setText(text("settings.iconsCustomization.size.helper"));
        spacingTitleLabel.setText(text("settings.iconsCustomization.spacing.title"));
        spacingHelperLabel.setText(text("settings.iconsCustomization.spacing.helper"));

        transparencyTitleLabel.setText(text("settings.dockCustomization.transparency.title"));
        transparencyHelperLabel.setText(text("settings.dockCustomization.transparency.helper"));
        roundingTitleLabel.setText(text("settings.dockCustomization.rounding.title"));
        roundingHelperLabel.setText(text("settings.dockCustomization.rounding.helper"));
        backgroundColorTitleLabel.setText(text("settings.dockCustomization.background.title"));
        backgroundColorHelperLabel.setText(text("settings.dockCustomization.background.helper"));

        positioningModeTitleLabel.setText(text("settings.positioning.mode.title"));
        positioningModeHelperLabel.setText(text("settings.positioning.mode.helper"));
        staticPositioningRadio.setText(text("settings.positioning.mode.static"));
        dynamicPositioningRadio.setText(text("settings.positioning.mode.dynamic"));
        alignmentTitleLabel.setText(text("settings.positioning.alignment.title"));
        verticalLabel.setText(text("settings.positioning.alignment.vertical"));
        horizontalLabel.setText(text("settings.positioning.alignment.horizontal"));
        screenSpacingTitleLabel.setText(text("settings.positioning.spacing.title"));
        topSpacingLabel.setText(text("settings.positioning.spacing.top"));
        leftSpacingLabel.setText(text("settings.positioning.spacing.left"));
        rightSpacingLabel.setText(text("settings.positioning.spacing.right"));
        bottomSpacingLabel.setText(text("settings.positioning.spacing.down"));
        dynamicPositioningTitleLabel.setText(text("settings.positioning.dynamic.title"));
        dynamicPositioningHelperLabel.setText(text("settings.positioning.dynamic.helper"));

        versionLabel.setText(text("settings.general.version"));
        repositoryLabel.setText(text("settings.general.repository"));
        contactLabel.setText(text("settings.general.contact"));
        openSourceLabel.setText(text("settings.general.openSource"));
        acknowledgementsButton.setText(text("settings.general.acknowledgements"));
        showDesktopProtectionTitleLabel.setText(text("settings.general.showDesktopProtection.title"));
        showDesktopProtectionHelperLabel.setText(text("settings.general.showDesktopProtection.helper"));
        showDesktopProtectionCheckBox.setText(text("settings.general.showDesktopProtection.enabled"));

        updateWindowTitle();
    }

    private void updateWindowTitle() {
        if (listView.getScene() != null && listView.getScene().getWindow() instanceof Stage stage && appServices != null) {
            stage.setTitle(text("settings.window.title"));
        }
    }

    private String verticalAnchorText(DockVerticalAnchor verticalAnchor) {
        return switch (verticalAnchor) {
            case TOP -> text("settings.positioning.choice.top");
            case MIDDLE -> text("settings.positioning.choice.middle");
            case DOWN -> text("settings.positioning.choice.down");
        };
    }

    private String horizontalAnchorText(DockHorizontalAnchor horizontalAnchor) {
        return switch (horizontalAnchor) {
            case LEFT -> text("settings.positioning.choice.left");
            case MIDDLE -> text("settings.positioning.choice.middle");
            case RIGHT -> text("settings.positioning.choice.right");
        };
    }

    private String text(String key) {
        return appServices.localizationService().text(key);
    }
}
