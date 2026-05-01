using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace LicorpExportPlus.Utils
{
    /// <summary>
    /// ObservableCollection with AddRange support
    /// Fires single CollectionChanged event instead of N events
    /// → Prevents UI thread blocking with large datasets
    /// </summary>
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        /// <summary>
        /// Default constructor
        /// </summary>
        public ObservableRangeCollection() : base()
        {
        }

        /// <summary>
        /// Constructor with initial collection
        /// </summary>
        public ObservableRangeCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        /// <summary>
        /// Add multiple items with single notification
        /// Key optimization: 1 UI update instead of N updates
        /// </summary>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            _suppressNotification = true;

            foreach (var item in collection)
            {
                Items.Add(item);
            }

            _suppressNotification = false;

            // Single notification to UI - prevents N×PropertyChanged events
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }
    }
}
