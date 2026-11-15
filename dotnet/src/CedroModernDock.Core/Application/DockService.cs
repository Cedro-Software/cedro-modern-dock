namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>
/// Direct port of DockService. Holds the dock model loaded from the repository
/// and persists changes on every mutating operation.
/// </summary>
public class DockService
{
    private readonly IDockRepository _repository;
    private readonly DockModel _dock;

    public event Action? ItemsChanged;

    public void SaveItemChanges()
    {
        SaveChanges();
        ItemsChanged?.Invoke();
    }

    public DockService(IDockRepository repository)
    {
        _repository = repository;
        _dock = repository.Load();
    }

    public DockModel GetDock() => _dock;

    public List<DockItem> GetItems() => _dock.Items;

    public void AddItem(DockItem item)
    {
        _dock.AddItem(item);
        SaveItemChanges();
    }

    public void InsertItem(int index, DockItem item)
    {
        _dock.InsertItem(index, item);
        SaveItemChanges();
    }

    public bool GetIconsLocked() => _dock.IconsLocked;

    public void SetIconsLocked(bool locked)
    {
        _dock.IconsLocked = locked;
        SaveChanges();
    }

    public void RemoveItem(int index)
    {
        if (index < 0 || index >= _dock.Items.Count || _dock.Items[index] is DockSettingsItemModel)
            return;
        _dock.RemoveItem(index);
        SaveItemChanges();
    }

    public void SwapItems(int firstItemIdx, int secondItemIdx)
    {
        _dock.SwapItems(firstItemIdx, secondItemIdx);
        SaveItemChanges();
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        _dock.MoveItem(fromIndex, toIndex);
        SaveItemChanges();
    }

    public void SetDockPosition(double positionX, double positionY)
    {
        _dock.SetDockPosition(
            DockPositioningService.SnapToPixel(positionX),
            DockPositioningService.SnapToPixel(positionY));
        SaveChanges();
    }

    public void SaveChanges() => _repository.Save(_dock);
}
