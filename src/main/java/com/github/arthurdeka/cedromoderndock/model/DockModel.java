package com.github.arthurdeka.cedromoderndock.model;

import com.github.arthurdeka.cedromoderndock.application.SupportedLanguage;
import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class DockModel {

    @Getter
    private List<DockItem> items = new ArrayList<>();
    private int iconsSize = 24;
    private int spacingBetweenIcons = 0;
    private double dockTransparency = 0.3;
    @Setter
    @Getter
    private int dockBorderRounding = 10;
    @Setter
    @Getter
    private String dockColorRGB = "0, 0, 0, ";
    @Getter
    private double dockPositionX;
    @Getter
    private double dockPositionY;
    @Getter
    @Setter
    private DockPositioningMode positioningMode = DockPositioningMode.STATIC;
    @Getter
    @Setter
    private DockVerticalAnchor verticalAnchor = DockVerticalAnchor.TOP;
    @Getter
    @Setter
    private DockHorizontalAnchor horizontalAnchor = DockHorizontalAnchor.MIDDLE;
    @Getter
    @Setter
    private int topSpacing = 20;
    @Getter
    @Setter
    private int leftSpacing = 20;
    @Getter
    @Setter
    private int rightSpacing = 20;
    @Getter
    @Setter
    private int bottomSpacing = 20;
    @Getter
    @Setter
    private SupportedLanguage language = SupportedLanguage.EN_US;
    @Getter
    @Setter
    private boolean showDesktopProtectionEnabled = true;

    public void addItem(DockItem item) {
        items.add(item);

    }

    public void removeItem(int index) {
        items.remove(index);

    }

    public void loadDefaultItems() {
        items.add(new DockSettingsItemModel());
    }

    public void swapItems(int firstItemIdx, int secondItemIdx) {
        Collections.swap(items, firstItemIdx, secondItemIdx);
    }

    public Integer getIconsSize() {
        return iconsSize;
    }

    public void setIconsSize(Integer iconsSize) {
        this.iconsSize = iconsSize;
    }

    public Integer getSpacingBetweenIcons() {
        return spacingBetweenIcons;
    }

    public void setSpacingBetweenIcons(Integer spacingBetweenIcons) {
        this.spacingBetweenIcons = spacingBetweenIcons;
    }

    public Double getDockTransparency() {
        return dockTransparency;
    }

    public void setDockTransparency(Double dockTransparency) {
        this.dockTransparency = dockTransparency;
    }

    public void setDockPosition(Double PositionX, Double PositionY) {
        this.dockPositionX = PositionX;
        this.dockPositionY = PositionY;
    }
}
