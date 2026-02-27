using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities.Extension;

namespace ActEditor.Tools.PaletteSheetGenerator {
	public static class SpsManager {
		public static void InitLists(TextBox[] searchBoxes, ListView[] lists, List<SpriteResource>[] resources, SelectionChangedEventHandler lvSelectionChanged) {
			for (int i = 0; i < lists.Length; i++) {
				ListView lv = lists[i];
				lv.SetValue(TextSearch.TextPathProperty, "DisplayName");

				ListViewDataTemplateHelper.GenerateListViewTemplateNew(lv, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
					new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "File name", DisplayExpression = "DisplayName", SearchGetAccessor = "DisplayName", IsFill = true, TextAlignment = TextAlignment.Left, ToolTipBinding = "ToolTip" }
				}, new DefaultListViewComparer<SpriteResource>(), new string[] { "Default", "{StaticResource TextForeground}" }, "generateHeader", "false", "overrideSizeRedraw", "true");

				lv.SelectionMode = SelectionMode.Single;
				lv.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
				lv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);

				lv.KeyDown += delegate {
					if (ApplicationShortcut.Is(ApplicationShortcut.Copy)) {
						StringBuilder builder = new StringBuilder();

						foreach (SpriteResource res in lv.SelectedItems) {
							builder.AppendLine(res.DisplayName);
						}

						Clipboard.SetText(builder.ToString());
					}
				};

				lv.SelectionChanged += lvSelectionChanged;

				var box = searchBoxes[i];
				string search;
				object oLock = new object();
				int i1 = i;

				lv.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
					ListViewItem item = lv.GetObjectAtPoint<ListViewItem>(e.GetPosition(lv)) as ListViewItem;

					if (item != null) {
						if (item.IsSelected)
							lvSelectionChanged(lv, null);
					}
				};

				box.TextChanged += delegate {
					search = box.Text;
					var current = search;

					Task.Run(() => {
						lock (oLock) {
							if (search != current) return;

							List<string> searchElements = current.ReplaceAll("  ", " ").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
							List<SpriteResource> result = new List<SpriteResource>(resources[i1]);

							if (searchElements.Count > 0) {
								result = result.Where(p => searchElements.All(s =>
									p.DisplayName.IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1 ||
									p.SpriteName.IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1)).ToList();

							}

							if (search != current) return;

							result = result.OrderBy(p => p.DisplayName).ToList();

							if (search != current) return;

							lv.Dispatch(p => p.ItemsSource = result);
							lv.Dispatch(delegate {
								try {
									ScrollViewer sv = (ScrollViewer)((Decorator)VisualTreeHelper.GetChild(lv, 0)).Child;
									sv.ScrollToTop();
								}
								catch { }
							});
						}
					});
				};
			}
		}
	}
}
