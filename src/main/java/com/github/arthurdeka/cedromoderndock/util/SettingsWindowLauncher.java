package com.github.arthurdeka.cedromoderndock.util;

import com.github.arthurdeka.cedromoderndock.App;
import com.github.arthurdeka.cedromoderndock.application.AppServices;
import com.github.arthurdeka.cedromoderndock.controller.SettingsController;
import com.github.arthurdeka.cedromoderndock.model.DockPositioningMode;
import javafx.fxml.FXMLLoader;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.stage.Stage;

import java.util.function.Consumer;

import static com.github.arthurdeka.cedromoderndock.util.UIUtils.setStageIcon;

public final class SettingsWindowLauncher {
    private SettingsWindowLauncher() {
    }

    public static void open(
            AppServices appServices,
            Runnable dockRefreshAction,
            Consumer<DockPositioningMode> positioningModeChangeAction,
            Consumer<Boolean> showDesktopProtectionChangeAction
    ) {
        try {
            FXMLLoader loader = new FXMLLoader(App.class.getResource("fxml/DockSettingsView.fxml"));
            Parent root = loader.load();

            SettingsController settingsController = loader.getController();
            settingsController.setAppServices(appServices);
            settingsController.setDockRefreshAction(dockRefreshAction);
            settingsController.setPositioningModeChangeAction(positioningModeChangeAction);
            settingsController.setShowDesktopProtectionChangeAction(showDesktopProtectionChangeAction);
            settingsController.handleInitialization();

            Stage stage = new Stage();
            stage.setTitle(appServices.localizationService().text("settings.window.title"));
            setStageIcon(stage);
            stage.setScene(new Scene(root));
            stage.show();
            stage.setMinHeight(stage.getHeight());
            stage.setMaxHeight(stage.getHeight());
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }
}
