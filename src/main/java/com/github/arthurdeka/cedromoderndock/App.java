package com.github.arthurdeka.cedromoderndock;

import com.github.arthurdeka.cedromoderndock.application.AppServices;
import com.github.arthurdeka.cedromoderndock.application.DockAppearanceService;
import com.github.arthurdeka.cedromoderndock.application.DockItemActionService;
import com.github.arthurdeka.cedromoderndock.application.DockPositioningService;
import com.github.arthurdeka.cedromoderndock.application.DockService;
import com.github.arthurdeka.cedromoderndock.application.LocalizationService;
import com.github.arthurdeka.cedromoderndock.application.SupportedLanguage;
import com.github.arthurdeka.cedromoderndock.application.WindowPreviewService;
import com.github.arthurdeka.cedromoderndock.controller.DockController;
import com.github.arthurdeka.cedromoderndock.infrastructure.persistence.JsonDockRepository;
import com.github.arthurdeka.cedromoderndock.infrastructure.system.CachedWindowsIconGateway;
import com.github.arthurdeka.cedromoderndock.infrastructure.system.DefaultFolderLauncher;
import com.github.arthurdeka.cedromoderndock.infrastructure.system.DefaultProgramLauncher;
import com.github.arthurdeka.cedromoderndock.infrastructure.system.DefaultWindowsModuleLauncher;
import com.github.arthurdeka.cedromoderndock.infrastructure.system.JnaWindowQueryGateway;
import com.github.arthurdeka.cedromoderndock.model.DockPositioningMode;
import com.github.arthurdeka.cedromoderndock.util.SettingsWindowLauncher;
import com.github.arthurdeka.cedromoderndock.util.SingleInstanceGuard;
import com.github.arthurdeka.cedromoderndock.util.NativeWindowUtils;
import com.github.arthurdeka.cedromoderndock.util.SystemTrayManager;
import javafx.application.Application;
import javafx.application.Platform;
import javafx.fxml.FXMLLoader;
import javafx.scene.Scene;
import javafx.scene.paint.Color;
import javafx.stage.Stage;
import javafx.stage.StageStyle;

import javax.swing.JOptionPane;
import java.io.IOException;

import static com.github.arthurdeka.cedromoderndock.util.UIUtils.setStageIcon;

public class App extends Application {
    private static SingleInstanceGuard singleInstanceGuard;
    private SystemTrayManager systemTrayManager;
    private AutoCloseable desktopDockWatcher;

    @Override
    public void start(Stage primaryStage) throws IOException {
        AppServices appServices = createServices();

        // creates a new stage for the dock
        Stage dockStage = new Stage();

        // loading dock interface and controller.
        FXMLLoader loader = new FXMLLoader(App.class.getResource("fxml/DockView.fxml"));
        Scene scene = new Scene(loader.load());

        // configuring dock stage.
        dockStage.setTitle("Cedro Modern Dock");
        setStageIcon(dockStage);
        dockStage.initStyle(StageStyle.TRANSPARENT);
        scene.setFill(Color.TRANSPARENT);
        dockStage.setScene(scene);

        DockController dockController = loader.getController();
        dockController.setStage(dockStage);
        dockController.setAppServices(appServices);
        dockController.setShowDesktopProtectionChangeAction(enabled ->
                handleShowDesktopProtectionChange(appServices, dockStage, enabled)
        );
        dockController.handleInitialization();

        dockStage.iconifiedProperty().addListener((observable, wasIconified, isIconified) -> {
            if (!isIconified) {
                return;
            }

            Platform.runLater(() -> {
                if (!appServices.dockService().getDock().isShowDesktopProtectionEnabled()) {
                    return;
                }
                dockStage.setIconified(false);
                if (!dockStage.isShowing()) {
                    dockStage.show();
                }
                NativeWindowUtils.configureDesktopDockWindow(dockStage);
                NativeWindowUtils.refreshDockZOrderForShowDesktop(dockStage);
            });
        });

        Platform.setImplicitExit(false);
        dockStage.show();
        NativeWindowUtils.configureDesktopDockWindow(dockStage);
        updateShowDesktopProtection(dockStage, appServices.dockService().getDock().isShowDesktopProtectionEnabled());
        appServices.positioningService().applyPosition(dockStage);
        systemTrayManager = new SystemTrayManager(
                () -> openSettingsWindow(appServices, dockController, dockStage),
                Platform::exit
        );
        systemTrayManager.install();
    }

    @Override
    public void stop() {
        if (systemTrayManager != null) {
            systemTrayManager.dispose();
            systemTrayManager = null;
        }
        if (desktopDockWatcher != null) {
            try {
                desktopDockWatcher.close();
            } catch (Exception ignored) {
                // Best effort cleanup during application shutdown.
            }
            desktopDockWatcher = null;
        }
        if (singleInstanceGuard != null) {
            singleInstanceGuard.close();
            singleInstanceGuard = null;
        }
    }

    public static void main(String[] args) {
        singleInstanceGuard = new SingleInstanceGuard();
        if (!singleInstanceGuard.tryAcquire()) {
            SupportedLanguage language = new JsonDockRepository().load().getLanguage();
            JOptionPane.showMessageDialog(
                    null,
                    LocalizationService.bootstrapText(language, "dialog.singleInstance.message"),
                    "Cedro Modern Dock",
                    JOptionPane.WARNING_MESSAGE
            );
            return;
        }

        try {
            launch();
        } finally {
            if (singleInstanceGuard != null) {
                singleInstanceGuard.close();
                singleInstanceGuard = null;
            }
        }
    }

    private AppServices createServices() {
        DockService dockService = new DockService(new JsonDockRepository());
        DockAppearanceService appearanceService = new DockAppearanceService(dockService);
        DockPositioningService positioningService = new DockPositioningService(dockService);
        LocalizationService localizationService = new LocalizationService(dockService);
        DockItemActionService itemActionService = new DockItemActionService(
                new DefaultProgramLauncher(),
                new DefaultFolderLauncher(),
                new DefaultWindowsModuleLauncher()
        );
        WindowPreviewService windowPreviewService = new WindowPreviewService(new JnaWindowQueryGateway());

        return new AppServices(
                dockService,
                appearanceService,
                positioningService,
                itemActionService,
                windowPreviewService,
                new CachedWindowsIconGateway(),
                localizationService
        );
    }

    private void openSettingsWindow(AppServices appServices, DockController dockController, Stage dockStage) {
        SettingsWindowLauncher.open(
                appServices,
                dockController::updateDockUI,
                positioningMode -> handlePositioningModeChange(appServices, dockStage, positioningMode),
                enabled -> handleShowDesktopProtectionChange(appServices, dockStage, enabled)
        );
    }

    private void handlePositioningModeChange(
            AppServices appServices,
            Stage dockStage,
            DockPositioningMode positioningMode
    ) {
        DockPositioningMode currentMode = appServices.positioningService().getPositioningMode();
        if (currentMode == DockPositioningMode.STATIC && positioningMode == DockPositioningMode.DYNAMIC) {
            appServices.dockService().setDockPosition(dockStage.getX(), dockStage.getY());
        }
        appServices.positioningService().setPositioningMode(positioningMode);
    }

    private void handleShowDesktopProtectionChange(AppServices appServices, Stage dockStage, boolean enabled) {
        appServices.dockService().getDock().setShowDesktopProtectionEnabled(enabled);
        appServices.dockService().saveChanges();
        updateShowDesktopProtection(dockStage, enabled);
    }

    private void updateShowDesktopProtection(Stage dockStage, boolean enabled) {
        if (desktopDockWatcher != null) {
            try {
                desktopDockWatcher.close();
            } catch (Exception ignored) {
                // Best effort cleanup when the Windows desktop watcher is toggled.
            }
            desktopDockWatcher = null;
        }

        if (!enabled) {
            NativeWindowUtils.disableShowDesktopProtection(dockStage);
            return;
        }

        NativeWindowUtils.refreshDockZOrderForShowDesktop(dockStage);
        desktopDockWatcher = NativeWindowUtils.keepDockVisibleWithShowDesktop(dockStage);
    }
}
