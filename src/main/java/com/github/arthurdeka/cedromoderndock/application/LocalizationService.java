package com.github.arthurdeka.cedromoderndock.application;

import com.github.arthurdeka.cedromoderndock.model.DockItem;
import com.github.arthurdeka.cedromoderndock.model.DockItemType;
import com.github.arthurdeka.cedromoderndock.model.DockSettingsItemModel;
import com.github.arthurdeka.cedromoderndock.model.DockWindowsModuleItemModel;

import java.text.MessageFormat;
import java.util.List;
import java.util.MissingResourceException;
import java.util.ResourceBundle;
import java.util.concurrent.CopyOnWriteArrayList;

public class LocalizationService {
    private static final String BUNDLE_BASE_NAME = "com.github.arthurdeka.cedromoderndock.i18n.messages";
    private static final ResourceBundle FALLBACK_BUNDLE = loadBundle(SupportedLanguage.EN_US);

    private final DockService dockService;
    private final List<Runnable> listeners = new CopyOnWriteArrayList<>();
    private ResourceBundle bundle;

    public LocalizationService(DockService dockService) {
        this.dockService = dockService;
        this.bundle = loadBundle(getCurrentLanguage());
    }

    public SupportedLanguage getCurrentLanguage() {
        SupportedLanguage language = dockService.getDock().getLanguage();
        return language == null ? SupportedLanguage.EN_US : language;
    }

    public void setLanguage(SupportedLanguage language) {
        SupportedLanguage resolvedLanguage = language == null ? SupportedLanguage.EN_US : language;
        if (resolvedLanguage == getCurrentLanguage()) {
            return;
        }

        dockService.getDock().setLanguage(resolvedLanguage);
        dockService.saveChanges();
        bundle = loadBundle(resolvedLanguage);
        notifyListeners();
    }

    public String text(String key, Object... arguments) {
        String pattern = resolveText(bundle, key);
        if (arguments == null || arguments.length == 0) {
            return pattern;
        }
        return MessageFormat.format(pattern, arguments);
    }

    public String languageDisplayName(SupportedLanguage language) {
        SupportedLanguage resolvedLanguage = language == null ? SupportedLanguage.EN_US : language;
        return resolvedLanguage.nativeDisplayName();
    }

    public String dockItemLabel(DockItem item) {
        if (item == null) {
            return "";
        }

        if (item.getType() == DockItemType.SETTINGS || item instanceof DockSettingsItemModel) {
            return text("dockItem.settings");
        }

        if (item.getType() == DockItemType.WINDOWS_MODULE && item instanceof DockWindowsModuleItemModel windowsModuleItem) {
            return switch (windowsModuleItem.getModule()) {
                case "mypc" -> text("windowsModule.myComputer");
                case "trash" -> text("windowsModule.recycleBin");
                case "ctrlpnl" -> text("windowsModule.controlPanel");
                case "pconfig" -> text("windowsModule.settings");
                default -> item.getLabel();
            };
        }

        return item.getLabel();
    }

    public void addListener(Runnable listener) {
        listeners.add(listener);
    }

    public void removeListener(Runnable listener) {
        listeners.remove(listener);
    }

    public static String bootstrapText(SupportedLanguage language, String key, Object... arguments) {
        try {
            ResourceBundle bundle = loadBundle(language == null ? SupportedLanguage.EN_US : language);
            String pattern = resolveText(bundle, key);
            if (arguments == null || arguments.length == 0) {
                return pattern;
            }
            return MessageFormat.format(pattern, arguments);
        } catch (MissingResourceException e) {
            return key;
        }
    }

    private void notifyListeners() {
        for (Runnable listener : listeners) {
            listener.run();
        }
    }

    private static ResourceBundle loadBundle(SupportedLanguage language) {
        return ResourceBundle.getBundle(BUNDLE_BASE_NAME, language.locale());
    }

    private static String resolveText(ResourceBundle bundle, String key) {
        if (bundle != null && bundle.containsKey(key)) {
            return bundle.getString(key);
        }
        if (FALLBACK_BUNDLE.containsKey(key)) {
            return FALLBACK_BUNDLE.getString(key);
        }
        return key;
    }
}
