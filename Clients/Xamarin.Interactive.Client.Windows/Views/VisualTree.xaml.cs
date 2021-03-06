//
// Authors:
//   Sandy Armstrong <sandy@xamarin.com>
//   Larry Ewing <lewing@xamarin.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

using Xamarin.Interactive.Remote;

namespace Xamarin.Interactive.Client.Windows.Views
{
    partial class VisualTree : UserControl
    {
        InspectView rootElement;
        InspectView manuallySelectedInspectView;

        public static readonly DependencyProperty RootElementProperty =
            DependencyProperty.Register ("RootElement",
                typeof (InspectView),
                typeof (VisualTree),
                new PropertyMetadata (null,
                    new PropertyChangedCallback (RootElementValueChanged)));

        //PropertyChanged event handler to get the old value
        private static void RootElementValueChanged (
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            var value = eventArgs.NewValue as InspectView;
            var view = dependencyObject as VisualTree;

            view.rootElement = value;
        }

        public InspectView RootElement
        {
            get { return (InspectView) GetValue (RootElementProperty); }
            set { SetValue (RootElementProperty, value); }
        }

        public static readonly DependencyProperty SelectedElementProperty =
            DependencyProperty.Register ("SelectedElement",
                typeof (InspectView),
                typeof (VisualTree),
                new PropertyMetadata (null,
                    new PropertyChangedCallback (SelectedElementValueChanged)));

        private static void SelectedElementValueChanged (
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs eventArgs)
        {
            var value = eventArgs.NewValue as InspectView;
            var view = dependencyObject as VisualTree;
            var root = value.Root;

            var path = GetInspectViewPath (root, value.Handle);

            if (path == null)
                return;

            view.manuallySelectedInspectView = path.Last ();
            var treeValue = view.treeView.SelectedValue as InspectView;

            if (treeValue?.Handle != value?.Handle)
                SelectItem (view.treeView, path);
        }

        public InspectView SelectedElement
        {
            get { return (InspectView) GetValue (SelectedElementProperty); }
            set { SetValue (SelectedElementProperty, value); }
        }

        public VisualTree ()
        {
            InitializeComponent ();
        }

        void TreeView_OnSelectedItemChanged (object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var value = e.NewValue as InspectView;
            var item = e.Source as TreeViewItem;

            Dispatcher.CurrentDispatcher.InvokeAsync (() => SelectedElement = value);
            e.Handled = true;
        }

        /// <summary>
        /// Get the orderered list of elements that make up the path that leads to the InsepctView with
        /// a Handle equal to viewHandle. The first element in the returned list is always the root, and the
        /// last element is the matching view.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="viewHandle"></param>
        /// <returns>Null if no matching view is found. List as described, otherwise.</returns>
        static List<InspectView> GetInspectViewPath (InspectView root, long viewHandle)
        {
            if (root.Handle == viewHandle)
                return new List<InspectView> { root };

            if (root.Subviews == null)
                return null;

            foreach (InspectView child in root.Subviews) {
                var path = GetInspectViewPath (child, viewHandle);
                if (path == null)
                    continue;

                path.Insert (0, root);
                return path;
            }

            return null;
        }

        static void ClearSelection (ItemsControl parentContainer)
        {
            if (parentContainer?.Items == null)
                return;

            foreach (var item in parentContainer.Items) {
                var itemContainer =
                    parentContainer.ItemContainerGenerator.ContainerFromItem (item) as TreeViewItem;

                if (itemContainer == null)
                    continue;

                itemContainer.IsSelected = false;

                ClearSelection (itemContainer);
            }
        }

        /// <summary>
        /// Select and scroll to a TreeViewItem, given a path of data items.
        /// </summary>
        /// <typeparam name="T">The data item type.</typeparam>
        /// <param name="parent">External callers should pass in the TreeView.</param>
        /// <param name="path">The path of data items. The first element should be the root, and the last
        /// element should be the final item to select.</param>
        static void SelectItem<T> (ItemsControl parent, List<T> path)
        {
            if (!(parent.ItemContainerGenerator.ContainerFromItem (
                path.First ()) is TreeViewItem container))
                return;

            var lastItem = path.GetRange (1, path.Count - 1);

            if (container.Items.Count == 0 || lastItem.Count == 0) {
                container.IsSelected = true;
                typeof (TreeViewItem)
                    .GetMethod (
                        "Select",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke (container, new object [] { true });
                container.BringIntoView ();
            } else {
                container.IsExpanded = true;
                if (container.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                    container.ItemContainerGenerator.StatusChanged += (o, e)
                        => SelectItem (container, lastItem);
                else
                    SelectItem (container, lastItem);
            }
        }
    }
}
