using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.Core;
using GRF.IO;
using GRF.Threading;
using GrfToWpfBridge;
using GrfToWpfBridge.TreeViewManager;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;
using Utilities.Extension;

namespace ActEditor.Tools.GrfShellExplorer {
	public enum SelectMode {
		Act,
		Pal
	}

	/// <summary>
	/// Interaction logic for GrfExplorer.xaml
	/// Class imported from GrfEditor
	/// </summary>
	public partial class GrfExplorer : Window {
		public SelectMode SelectMode;
		private readonly string _filename;
		private readonly GrfHolder _grfHolder = new GrfHolder();
		private readonly object _listLoadLock = new object();
		private FileEntry _latestSelectedItem;
		private TreeViewPathManager _treeViewPathManager;

		public GrfExplorer() {
			InitializeComponent();
		}

		private void _grfExplorer_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				Close();
			}
		}

		private void _onPreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				_buttonOk_Click(null, null);
				e.Handled = true;
			}
		}

		public GrfExplorer(string filename, SelectMode mode) {
			SelectMode = mode;
			_filename = filename;
			InitializeComponent();

			_loadEditorUI();
			_load(_filename);

			WindowStartupLocation = WindowStartupLocation.CenterOwner;
		}

		public string SelectedItem {
			get { return _latestSelectedItem == null ? null : _latestSelectedItem.RelativePath; }
		}

		private void _loadEditorUI() {
			_treeViewPathManager = new TreeViewPathManager(_treeView);
			ShowInTaskbar = false;

			_items.ItemsSource = _itemEntries;
			_listBoxResults.ItemsSource = _itemSearchEntries;
			_treeView.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(_treeView_PreviewMouseLeftButtonDown);
			_listBoxResults.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(_listBoxResults_PreviewMouseLeftButtonDown);

			ListViewDataTemplateHelper.GenerateListViewTemplate(_listBoxResults, new ListViewDataTemplateHelper.ColumnInfo[] {
				new ListViewDataTemplateHelper.ColumnInfo {Header = "", DisplayExpression = "{Binding Path=DataImage}", SearchGetAccessor = "FileType", Alignment = TextAlignment.Center, Width = 20, MaxHeight = 24, IsImage = true, Margin = "-4"},
				new ListViewDataTemplateHelper.ColumnInfo {Header = "File name", DisplayExpression = "{Binding Path=RelativePath}", SearchGetAccessor = "RelativePath", Alignment = TextAlignment.Left, Width = -1},
				new ListViewDataTemplateHelper.ColumnInfo {Header = "Type", DisplayExpression = "{Binding Path=FileType}", SearchGetAccessor = "FileType", Alignment = TextAlignment.Right, Width = 40},
				new ListViewDataTemplateHelper.ColumnInfo {Header = "Size", DisplayExpression = "{Binding Path=DisplaySize}", SearchGetAccessor = "NewSizeDecompressed", Alignment = TextAlignment.Right, Width = 60}
			}, new DefaultListViewComparer<FileEntry>(), new string[] {"Added", "Blue", "Encrypted", "#FFE08000", "Removed", "Red"});

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_items, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.ImageColumnInfo {Header = "", DisplayExpression = "DataImage", SearchGetAccessor = "FileType", FixedWidth = 20, MaxHeight = 24},
				new ListViewDataTemplateHelper.RangeColumnInfo {Header = "File name", DisplayExpression = "DisplayRelativePath", SearchGetAccessor = "RelativePath", IsFill = true, TextAlignment = TextAlignment.Left, ToolTipBinding = "RelativePath", MinWidth = 100},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Type", DisplayExpression = "FileType", FixedWidth = 40, ToolTipBinding = "FileType", TextAlignment = TextAlignment.Right},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Size", DisplayExpression = "DisplaySize", SearchGetAccessor = "NewSizeDecompressed", FixedWidth = 60, TextAlignment = TextAlignment.Right, ToolTipBinding = "NewSizeDecompressed"}
			}, new DefaultListViewComparer<FileEntry>(), new string[] {"Added", "Blue", "Encrypted", "#FFE08000", "Removed", "Red"});

			WpfUtils.AddDragDropEffects(_items);
			WpfUtils.AddDragDropEffects(_treeView, f => f.Select(p => p.GetExtension()).All(p => p == ".grf" || p == ".rgz" || p == ".thor" || p == ".gpf"));

			_grfEntrySorter.SetOrder("DisplayRelativePath", ListSortDirection.Ascending);
			_grfSearchEntrySorter.SetOrder("RelativePath", ListSortDirection.Ascending);

			_items.PreviewKeyDown += _onPreviewKeyDown;
			_listBoxResults.PreviewKeyDown += _onPreviewKeyDown;
			PreviewKeyDown += new KeyEventHandler(_grfExplorer_KeyDown);
		}

		private void _listBoxResults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			try {
				ListViewItem item = _listBoxResults.GetObjectAtPoint<ListViewItem>(e.GetPosition(_listBoxResults));

				if (item != null) {
					if (item.IsSelected)
						_listBoxResults_SelectionChanged(item, null);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		//#region Tree view interactions
		private void _treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
			TkTreeViewItem item = _treeView.SelectedItem as TkTreeViewItem;

			if (item != null) {
				_loadListItems();
				_showPreview(_grfHolder, _treeViewPathManager.GetCurrentRelativePath(), null);
				ActEditorConfiguration.GrfShellLatest = _treeViewPathManager.GetCurrentRelativePath();
			}
		}

		private void _showPreview(GrfHolder grfData, string currentPath, string selectedItem) {
			if (selectedItem != null) {
				if (selectedItem.IsExtension(".spr", ".jpg", ".png", ".tga", ".bmp", ".pal")) {
					_previewImage.Load(grfData, grfData.FileTable[GrfPath.Combine(currentPath, selectedItem)]);
					_previewImage.Visibility = Visibility.Visible;
					_previewAct.Visibility = Visibility.Hidden;
				}
				else if (selectedItem.IsExtension(".act")) {
					_previewAct.Load(grfData, grfData.FileTable[GrfPath.Combine(currentPath, selectedItem)]);
					_previewImage.Visibility = Visibility.Hidden;
					_previewAct.Visibility = Visibility.Visible;
				}
			}
		}

		private void _treeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			TreeViewItem item = WpfUtilities.GetTreeViewItemClicked((FrameworkElement) e.OriginalSource, _treeView);
			if (item != null) {
				item.IsSelected = true;
			}
		}

		private void _treeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			TreeViewItem item = WpfUtilities.GetTreeViewItemClicked((FrameworkElement) e.OriginalSource, _treeView);

			if (item != null && item == _treeView.SelectedItem) {
				_treeView_SelectedItemChanged(sender, null);
			}
		}

		private void _gridBoxResultsHeight_SizeChanged(object sender, SizeChangedEventArgs e) {
			if (_gridSearchResults.Visibility == Visibility.Visible)
				_gridSearchResults.Height = _gridBoxResultsHeight.ActualHeight;
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			if (_latestSelectedItem != null) {
				DialogResult = true;
			}
			else {
				ErrorHandler.HandleException("No item selected.");
				return;
			}

			Close();
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}

		private void _listBoxResults_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left) {
				ListViewItem item = _listBoxResults.GetObjectAtPoint<ListViewItem>(e.GetPosition(_listBoxResults));

				if (item != null && _latestSelectedItem != null) {
					if (SelectMode == SelectMode.Act) {
						if (_latestSelectedItem.RelativePath.IsExtension(".act")) {
							e.Handled = true;
							_buttonOk_Click(null, null);
						}
						else if (_latestSelectedItem.RelativePath.IsExtension(".spr")) {
							var entry = _grfHolder.FileTable.TryGet(_latestSelectedItem.RelativePath.ReplaceExtension(".act"));

							if (entry != null) {
								e.Handled = true;
								_latestSelectedItem = entry;
								_buttonOk_Click(null, null);
							}
						}
					}
					else if (SelectMode == SelectMode.Pal) {
						if (_latestSelectedItem.RelativePath.IsExtension(".spr", ".pal")) {
							e.Handled = true;
							_buttonOk_Click(null, null);
						}
					}
				}
			}
		}

		private void _items_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left) {
				ListViewItem item = _items.GetObjectAtPoint<ListViewItem>(e.GetPosition(_items));

				if (item != null && _latestSelectedItem != null) {
					if (SelectMode == SelectMode.Act) {
						if (_latestSelectedItem.RelativePath.IsExtension(".act")) {
							e.Handled = true;
							_buttonOk_Click(null, null);
						}
						else if (_latestSelectedItem.RelativePath.IsExtension(".spr")) {
							var entry = _grfHolder.FileTable.TryGet(_latestSelectedItem.RelativePath.ReplaceExtension(".act"));

							if (entry != null) {
								e.Handled = true;
								_latestSelectedItem = entry;
								_buttonOk_Click(null, null);
							}
						}
					}
					else if (SelectMode == SelectMode.Pal) {
						if (_latestSelectedItem.RelativePath.IsExtension(".spr", ".pal")) {
							e.Handled = true;
							_buttonOk_Click(null, null);
						}
					}
				}
			}
		}

		#region TreeView loading (logic)

		private void _load(string filename) {
			new Thread(new ThreadStart(delegate {
				try {
					_listBoxResults.Dispatch(p => _itemSearchEntries.Clear());
					_items.Dispatch(p => _itemEntries.Clear());
					_treeViewPathManager.ClearAll();

					_treeViewPathManager.ClearCommands();
					_grfHolder.Close();

					_grfHolder.Open(filename);

					if (_grfHolder.Header == null) {
						this.Dispatch(p => p.Title = "GrfShell Explorer");
						_treeViewPathManager.ClearAll();
						_grfHolder.Close();
					}
					else {
						this.Dispatch(p => p.Title = "GrfShell Explorer - " + Methods.CutFileName(filename));

						_treeViewPathManager.AddPath(new TkPath {FilePath = filename, RelativePath = ""});

						foreach (string pathname in _grfHolder.FileTable.Directories) {
							_treeViewPathManager.AddPath(new TkPath {FilePath = filename, RelativePath = pathname});
						}

						var latestPath = String.IsNullOrEmpty(ActEditorConfiguration.GrfShellLatest) ? "data\\sprite" : ActEditorConfiguration.GrfShellLatest;

						_treeViewPathManager.ExpandFirstNode();
						_treeViewPathManager.SelectFirstNode();

						List<string> paths = Methods.StringToList("data,data\\sprite");

						foreach (string path in paths) {
							_treeViewPathManager.Expand(new TkPath {FilePath = filename, RelativePath = path});
						}

						_treeViewPathManager.Select(new TkPath { FilePath = filename, RelativePath = latestPath });

						_search();
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
			})) {Name = "GrfEditor - GRF loading thread"}.Start();
		}

		#endregion

		#region Search

		private readonly object _filterLock = new object();
		private readonly FileEntryComparer<FileEntry> _grfEntrySorter = new FileEntryComparer<FileEntry>();
		private readonly FileEntryComparer<FileEntry> _grfSearchEntrySorter = new FileEntryComparer<FileEntry>();
		private readonly object _searchLock = new object();
		private ObservableCollection<FileEntry> _itemEntries = new ObservableCollection<FileEntry>();
		private ObservableCollection<FileEntry> _itemSearchEntries = new ObservableCollection<FileEntry>();
		private string _searchFilter = "";
		private string _searchSelectedPath = "";
		private string _searchString = "";

		#region Search ListView interactions

		private void _listBoxResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				if (_listBoxResults.SelectedItem != null) {
					_latestSelectedItem = _listBoxResults.SelectedItem as FileEntry;
					_showPreview(_grfHolder, Path.GetDirectoryName(_listBoxResults.SelectedItem.ToString()),
					             Path.GetFileName(_listBoxResults.SelectedItem.ToString()));
					ActEditorConfiguration.GrfShellLatest = _latestSelectedItem == null ? "" : Path.GetDirectoryName(_latestSelectedItem.RelativePath);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err.Message, ErrorLevel.Warning);
			}
		}

		#endregion

		#region Search interactions

		protected override void OnClosing(CancelEventArgs e) {
			_grfHolder.Close();
			base.OnClosing(e);
		}

		private void _textBoxSearch_TextChanged(object sender, TextChangedEventArgs e) {
			_searchFilter = _textBoxSearch.Text;
			_filter();
		}

		private void _textBox_TextChanged(object sender, EventArgs keyEventArgs) {
			_searchString = _textBoxMainSearch.Text;
			_search();
		}
		
		private void _search() {
			string currentSearch = _searchString;

			new Thread(new ThreadStart(delegate {
				lock (_searchLock) {
					try {
						if (currentSearch != _searchString)
							return;

						if (currentSearch == "") {
							_gridSearchResults.Dispatch(p => p.Visibility = Visibility.Collapsed);
							return;
						}

						if (currentSearch.Split(' ').All(p => p.Length == 0))
							return;

						//_listBoxResults.Dispatch(p => _itemSearchEntries.Clear());
						_gridSearchResults.Dispatch(p => p.Visibility = Visibility.Visible);

						this.Dispatch(p => p._grfSearchEntrySorter.SetOrder(WpfUtils.GetLastGetSearchAccessor(_listBoxResults), WpfUtils.GetLastSortDirection(_listBoxResults)));

						List<KeyValuePair<string, FileEntry>> entries = _grfHolder.FileTable.FastAccessEntries;
						List<string> search = currentSearch.Split(' ').ToList();
						_itemSearchEntries = new ObservableCollection<FileEntry>(entries.Where(p => search.All(q => p.Key.IndexOf(q, StringComparison.InvariantCultureIgnoreCase) != -1)).Select(p => p.Value).OrderBy(p => p, _grfSearchEntrySorter));
						_itemSearchEntries.Where(p => p.DataImage == null).ToList().ForEach(p => p.DataImage = IconProvider.GetSmallIcon(p.RelativePath));
						_listBoxResults.Dispatch(p => p.ItemsSource = _itemSearchEntries);
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err.Message, ErrorLevel.Warning);
					}
				}
			})) {Name = "GrfEditor - Search filter for all items thread"}.Start();
		}

		private void _filter(string currentPath = null) {
			string currentSearch = _searchFilter;

			new Thread(new ThreadStart(delegate {
				lock (_filterLock) {
					try {
						if (currentSearch != _searchFilter)
							return;

						if ((bool) _treeView.Dispatcher.Invoke(new Func<bool>(() => _treeView.SelectedItem == null)))
							return;

						if (currentPath != null && (_searchSelectedPath != currentPath || _searchSelectedPath == null))
							return;

						//GC.Collect();

						if (_items == null) return;

						this.Dispatch(p => p._grfEntrySorter.SetOrder(WpfUtils.GetLastGetSearchAccessor(_items), WpfUtils.GetLastSortDirection(_items)));

						List<Utilities.Extension.Tuple<string, string, FileEntry>> entries = _grfHolder.FileTable.FastTupleAccessEntries;
						List<string> search = currentSearch.Split(' ').ToList();
						_itemEntries = new ObservableCollection<FileEntry>(entries.Where(p => p.Item1 == _searchSelectedPath && search.All(q => p.Item2.IndexOf(q, StringComparison.InvariantCultureIgnoreCase) != -1)).Select(p => p.Item3).OrderBy(p => p, _grfEntrySorter));
						_itemEntries.Where(p => p.DataImage == null).ToList().ForEach(p => p.DataImage = IconProvider.GetSmallIcon(p.RelativePath));
						_items.Dispatch(p => p.ItemsSource = _itemEntries);
					}
					catch {
					}
				}
			})) {Name = "GrfEditor - Search filter for ListView items thread"}.Start();
		}

		public class FileEntryComparer<T> : IComparer<T> {
			private readonly DefaultListViewComparer<T> _internalSearch = new DefaultListViewComparer<T>();
			private string _searchGetAccessor;

			#region IComparer<T> Members

			public int Compare(T x, T y) {
				if (_searchGetAccessor != null)
					return _internalSearch.Compare(x, y);

				return 0;
			}

			#endregion

			public void SetOrder(string searchGetAccessor, ListSortDirection direction) {
				if (searchGetAccessor != null) {
					_searchGetAccessor = searchGetAccessor;
					_internalSearch.SetSort(searchGetAccessor, direction);
				}
			}
		}

		#endregion

		#endregion

		#region List view interaction

		#region Events

		private void _items_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_items.SelectedItem == null) {
				return;
			}

			_latestSelectedItem = (FileEntry) _items.SelectedItem;
			FileEntry entry = (FileEntry) _items.SelectedItem;
			_showPreview(_grfHolder, Path.GetDirectoryName(entry.RelativePath), Path.GetFileName(entry.RelativePath));
			ActEditorConfiguration.GrfShellLatest = _latestSelectedItem == null ? "" : Path.GetDirectoryName(_latestSelectedItem.RelativePath);
		}

		private void _items_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			try {
				ListViewItem item = _items.GetObjectAtPoint<ListViewItem>(e.GetPosition(_items));

				if (item != null && item.IsSelected) {
					_items_SelectionChanged(sender, null);
				}

				e.Handled = false;
			}
			catch {
			}
		}

		#endregion

		private void _loadListItems() {
			string currentPath = _treeViewPathManager.GetCurrentRelativePath();

			_treeView.Dispatcher.Invoke((Action) delegate {
				if (_treeView.SelectedItem == null) {
					_itemEntries.Clear();
				}

				_searchSelectedPath = _treeViewPathManager.GetCurrentRelativePath();
			});

			GrfThread.Start(delegate {
				lock (_listLoadLock) {
					_searchFilter = (string) _textBoxSearch.Dispatcher.Invoke(new Func<string>(() => _textBoxSearch.Text));
					_filter(currentPath);
				}
			}, "GrfEditor - Search filter for ListView thread");
		}

		#endregion
	}
}