namespace LocaLink.Controls;

using System.Collections.Specialized;

public class AutoScrollListView : ListView
{
    public AutoScrollListView()
    {
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ItemsSource) &&
                ItemsSource is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged += OnItemsSourceCollectionChanged;
            }
        };
    }

    private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace))
            return;

        if (e.NewItems == null || ItemsSource == null)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Wait for UI to finish layout
            await Task.Delay(100);

            var lastItem = e.NewItems[e.NewItems.Count - 1];
            ScrollTo(lastItem, ScrollToPosition.End, true);
        });
    }
}