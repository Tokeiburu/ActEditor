using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using GrfToWpfBridge.MultiGrf;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Utilities.Services;
using Binder = GrfToWpfBridge.Binder;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ActEditorSettings.xaml
	/// </summary>
	public partial class ActEditorSettings : TkWindow {
		public ActEditorSettings() {
			InitializeComponent();
		}

		public ActEditorSettings(MetaGrfResourcesViewer resource) : base("Settings", "settings.png") {
			InitializeComponent();
			ActEditorConfiguration.ConfigAsker.AdvancedSettingEnabled = true;

			_colorPreviewPanelBakground.Color = ActEditorConfiguration.ActEditorBackgroundColor;
			_colorPreviewPanelBakground.Init(ActEditorConfiguration.ConfigAsker.RetrieveSetting(() => ActEditorConfiguration.ActEditorBackgroundColor));

			_colorPreviewPanelBakground.ColorChanged += delegate(object sender, Color value) {
				ActEditorConfiguration.ActEditorBackgroundColor = value;

				foreach (var tab in ActEditorWindow.Instance._tabControl.Items.OfType<TabAct>()) {
					tab._rendererPrimary._primary.Background = new SolidColorBrush(value);
				}
			};

			_colorPreviewPanelBakground.PreviewColorChanged += delegate(object sender, Color value) {
				ActEditorConfiguration.ActEditorBackgroundColor = value;
				ActEditorWindow.Instance.GetCurrentTab2()._rendererPrimary._primary.Background = new SolidColorBrush(value);
			};

			_set(_colorSpritePanelBackground, () => ActEditorConfiguration.ActEditorSpriteBackgroundColor, v => ActEditorConfiguration.ActEditorSpriteBackgroundColor = v);
			_set(_colorGridLH, () => ActEditorConfiguration.ActEditorGridLineHorizontal, v => ActEditorConfiguration.ActEditorGridLineHorizontal = v);
			_set(_colorGridLV, () => ActEditorConfiguration.ActEditorGridLineVertical, v => ActEditorConfiguration.ActEditorGridLineVertical = v);
			_set(_colorSpriteBorder, () => ActEditorConfiguration.ActEditorSpriteSelectionBorder, v => ActEditorConfiguration.ActEditorSpriteSelectionBorder = v);
			_set(_colorSpriteOverlay, () => ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay, v => ActEditorConfiguration.ActEditorSpriteSelectionBorderOverlay = v);
			_set(_colorSelectionBorder, () => ActEditorConfiguration.ActEditorSelectionBorder, v => ActEditorConfiguration.ActEditorSelectionBorder = v);
			_set(_colorSelectionOverlay, () => ActEditorConfiguration.ActEditorSelectionBorderOverlay, v => ActEditorConfiguration.ActEditorSelectionBorderOverlay = v);
			_set(_colorAnchor, () => ActEditorConfiguration.ActEditorAnchorColor, v => ActEditorConfiguration.ActEditorAnchorColor = v);

			Binder.Bind(_gridHVisible, () => ActEditorConfiguration.ActEditorGridLineHVisible, v => ActEditorConfiguration.ActEditorGridLineHVisible = v, () => ActEditorWindow.Instance.GetCurrentTab2()._rendererPrimary.UpdateAndSelect());
			Binder.Bind(_gridVVisible, () => ActEditorConfiguration.ActEditorGridLineVVisible, v => ActEditorConfiguration.ActEditorGridLineVVisible = v, () => ActEditorWindow.Instance.GetCurrentTab2()._rendererPrimary.UpdateAndSelect());
			Binder.Bind(_cbRefresh, () => ActEditorConfiguration.ActEditorRefreshLayerEditor, v => ActEditorConfiguration.ActEditorRefreshLayerEditor = v);
			Binder.Bind(_cbAliasing, () => ActEditorConfiguration.UseAliasing, v => ActEditorConfiguration.UseAliasing = v, () => ActEditorWindow.Instance.GetCurrentTab2()._rendererPrimary.UpdateAndSelect());

			Binder.Bind(_debuggerLogAnyExceptions, () => Configuration.LogAnyExceptions, v => Configuration.LogAnyExceptions = v);

			Binder.Bind(_cbUniform, () => ActEditorConfiguration.ActEditorGifUniform, v => ActEditorConfiguration.ActEditorGifUniform = v);
			Binder.Bind(_colorBackground, () => ActEditorConfiguration.ActEditorGifBackgroundColor, v => ActEditorConfiguration.ActEditorGifBackgroundColor = v);
			Binder.Bind(_colorGuildelines, () => ActEditorConfiguration.ActEditorGifGuidelinesColor, v => ActEditorConfiguration.ActEditorGifGuidelinesColor = v);
			Binder.Bind(_cbHideGifDialog, () => ActEditorConfiguration.ActEditorGifHideDialog, v => ActEditorConfiguration.ActEditorGifHideDialog = v);

			Binder.Bind(_tbDelayFactor, () => ActEditorConfiguration.ActEditorGifDelayFactor, v => ActEditorConfiguration.ActEditorGifDelayFactor = v);
			Binder.Bind(_tbMargin, () => ActEditorConfiguration.ActEditorGifMargin, v => ActEditorConfiguration.ActEditorGifMargin = v);
			Binder.Bind(_gridReopenLastest, () => ActEditorConfiguration.ReopenLatestFile, v => ActEditorConfiguration.ReopenLatestFile = v);

			_mz1.SelectedIndex = ActEditorConfiguration.ActEditorZoomInMultiplier > 0 ? 0 : 1;
			_mz2.SelectedIndex = ActEditorConfiguration.ActEditorZoomInMultiplier > 0 ? 1 : 0;

			bool enableEvents = true;

			_mz1.SelectionChanged += delegate {
				if (!enableEvents) return;

				ActEditorConfiguration.ActEditorZoomInMultiplier = _mz1.SelectedIndex == 0 ? 1 : -1;

				enableEvents = false;
				_mz2.SelectedIndex = _mz1.SelectedIndex == 0 ? 1 : 0;
				enableEvents = true;
			};

			_mz2.SelectionChanged += delegate {
				if (!enableEvents) return;

				ActEditorConfiguration.ActEditorZoomInMultiplier = _mz2.SelectedIndex == 0 ? -1 : 1;

				enableEvents = false;
				_mz1.SelectedIndex = _mz2.SelectedIndex == 0 ? 1 : 0;
				enableEvents = true;
			};

			_resourceGrfs.Children.Add(resource);
			resource.Height = 150;

			_comboBoxEncoding.Items.Add("Default (codeage 1252 - Western European [Windows])");
			_comboBoxEncoding.Items.Add("Korean (codepage 949 - ANSI/OEM Korean [Unified Hangul Code])");
			_comboBoxEncoding.Items.Add("Other...");

			switch (ActEditorConfiguration.EncodingCodepage) {
				case 1252:
					_comboBoxEncoding.SelectedIndex = 0;
					break;
				case 949:
					_comboBoxEncoding.SelectedIndex = 1;
					break;
				default:
					_comboBoxEncoding.Items[2] = ActEditorConfiguration.EncodingCodepage + "...";
					_comboBoxEncoding.SelectedIndex = 2;
					break;
			}

			_comboBoxEncoding.SelectionChanged += _comboBoxEncoding_SelectionChanged;

			Binder.Bind(_assAct, () => ActEditorConfiguration.ShellAssociateAct, v => ActEditorConfiguration.ShellAssociateAct = v, delegate {
				if (ActEditorConfiguration.ShellAssociateAct) {
					try {
						ApplicationManager.AddExtension(Methods.ApplicationFullPath, "Act", ".act", true);
					}
					catch (Exception err) {
						ErrorHandler.HandleException("Failed to associate the file extension\n\n" + err.Message, ErrorLevel.NotSpecified);
					}
				}
				else {
					try {
						ApplicationManager.RemoveExtension("grfeditor", ".act");
					}
					catch (Exception err) {
						ErrorHandler.HandleException("Failed to remove the association of the file extension\n\n" + err.Message, ErrorLevel.NotSpecified);
					}
				}
			});

			_comboBoxStyles.Items.Add("Default");
			_comboBoxStyles.Items.Add("Dark theme");

			if (Directory.Exists(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes"))) {	
				foreach (var file in Directory.GetFiles(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes"), "*.xaml")) {
					_comboBoxStyles.Items.Add(Path.GetFileNameWithoutExtension(file));
				}
			}

			var name = ActEditorConfiguration.StyleTheme == "" ? "Default" : (ActEditorConfiguration.StyleTheme == "StyleDark.xaml" ? "Dark theme" : ActEditorConfiguration.StyleTheme);

			_comboBoxStyles.SelectedItem = name;
			_comboBoxStyles.SelectionChanged += delegate {
				if (_comboBoxStyles.SelectedItem == null)
					return;

				try {
					var theme = _comboBoxStyles.SelectedItem.ToString();

					while (Application.Current.Resources.MergedDictionaries.Count > 2) {
						Application.Current.Resources.MergedDictionaries.RemoveAt(2);
					}

					if (theme == "Default") {
						ActEditorConfiguration.StyleTheme = "";
						return;
					}

					Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/StyleDark.xaml", UriKind.RelativeOrAbsolute) });
					ActEditorConfiguration.StyleTheme = "Dark theme";

					if (theme != "Dark theme") {
						ActEditorConfiguration.StyleTheme = theme;

						if (Directory.Exists(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes"))) {
							Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes", theme + ".xaml"), UriKind.RelativeOrAbsolute) });
						}
					}
				}
				finally {
					ErrorHandler.HandleException("For the theme to apply properly, please restart the application.");
				}
			};

			_loadShortcuts();
		}

		private void _set(QuickColorSelector qcs, Func<GrfColor> get, Action<GrfColor> set) {
			qcs.Color = get().ToColor();
			qcs.Init(ActEditorConfiguration.ConfigAsker.RetrieveSetting(() => get()));

			qcs.ColorChanged += delegate(object sender, Color value) {
				set(value.ToGrfColor());

				foreach (var tab in ActEditorWindow.Instance._tabControl.Items.OfType<TabAct>()) {
					tab._rendererPrimary.SizeUpdate();
				}
			};

			qcs.PreviewColorChanged += delegate(object sender, Color value) {
				ActEditorConfiguration.ConfigAsker.IsAutomaticSaveEnabled = false;
				set(value.ToGrfColor());
				ActEditorConfiguration.ConfigAsker.IsAutomaticSaveEnabled = true;
				ActEditorWindow.Instance.GetCurrentTab2()._rendererPrimary.SizeUpdate();
			};
		}

		private void _set(QuickColorSelector qcs, Func<Color> get, Action<Color> set) {
			qcs.Color = get();
			qcs.Init(ActEditorConfiguration.ConfigAsker.RetrieveSetting(() => get()));

			qcs.ColorChanged += delegate(object sender, Color value) {
				set(value);

				foreach (var tab in ActEditorWindow.Instance._tabControl.Items.OfType<TabAct>()) {
					tab._spriteSelector._dp.Background = new SolidColorBrush(value);
				}
			};

			qcs.PreviewColorChanged += delegate(object sender, Color value) {
				ActEditorConfiguration.ConfigAsker.IsAutomaticSaveEnabled = false;
				set(value);
				ActEditorConfiguration.ConfigAsker.IsAutomaticSaveEnabled = true;
				ActEditorWindow.Instance.GetCurrentTab2()._spriteSelector._dp.Background = new SolidColorBrush(value);
			};
		}

		private void _comboBoxEncoding_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			object oldSelected = null;
			bool cancel = false;

			if (e.RemovedItems.Count > 0)
				oldSelected = e.RemovedItems[0];

			switch (_comboBoxEncoding.SelectedIndex) {
				case 0:
					if (!_setEncoding(1252)) cancel = true;
					break;
				case 1:
					if (!_setEncoding(949)) cancel = true;
					break;
				case 2:
					InputDialog dialog = WindowProvider.ShowWindow<InputDialog>(new InputDialog("Using an unsupported encoding may cause unexpected results, make a copy of your GRF file before saving!\nEnter the codepage number for the encoding :",
					                                                                            "Encoding", _comboBoxEncoding.Items[2].ToString().IndexOf(' ') > 0 ? _comboBoxEncoding.Items[2].ToString().Substring(0, _comboBoxEncoding.Items[2].ToString().IndexOf(' ')) : EncodingService.DisplayEncoding.CodePage.ToString(CultureInfo.InvariantCulture)), this);

					bool pageExists;

					if (dialog.DialogResult == true) {
						pageExists = EncodingService.EncodingExists(dialog.Input);

						if (pageExists) {
							_comboBoxEncoding.SelectionChanged -= _comboBoxEncoding_SelectionChanged;
							_comboBoxEncoding.Items[2] = dialog.Input + "...";
							_comboBoxEncoding.SelectedIndex = 2;
							_comboBoxEncoding.SelectionChanged += _comboBoxEncoding_SelectionChanged;
							if (!_setEncoding(Int32.Parse(dialog.Input))) cancel = true;
						}
						else {
							cancel = true;
						}
					}
					else {
						cancel = true;
					}

					break;
				default:
					if (!_setEncoding(1252)) cancel = true;
					break;
			}

			if (cancel) {
				_comboBoxEncoding.SelectionChanged -= _comboBoxEncoding_SelectionChanged;

				if (oldSelected != null) {
					_comboBoxEncoding.SelectedItem = oldSelected;
				}

				_comboBoxEncoding.SelectionChanged += _comboBoxEncoding_SelectionChanged;
			}
		}

		public class ShortcutVisual {
			public Grid Grid;
			public Label Label;
		}

		private readonly Dictionary<string, ShortcutVisual> _shortcuts = new Dictionary<string, ShortcutVisual>();

		private void _loadShortcuts() {
			int row = 0;

			foreach (var keyPair in ApplicationShortcut.KeyBindings2) {
				string actionName = keyPair.Key;

				if (actionName == "#auto_generated")
					continue;

				var binding = keyPair.Value;

				Label l = new Label { Content = actionName };

				l.Content = actionName;

				WpfUtilities.SetGridPosition(l, row, 0);
				_gridShortcuts.Children.Add(l);

				Border b = new Border();
				b.Margin = new Thickness(3);
				b.BorderThickness = new Thickness(1);
				b.BorderBrush = WpfUtilities.LostFocusBrush;

				Grid grid = new Grid();
				_shortcuts[actionName] = new ShortcutVisual { Grid = grid, Label = l };
				grid.ColumnDefinitions.Add(new ColumnDefinition());
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(-1, GridUnitType.Auto) });

				TextBox tb = new TextBox { Text = binding.KeyGesture.DisplayString };
				tb.BorderThickness = new Thickness(0);
				tb.Padding = new Thickness(0);
				tb.IsReadOnly = true;
				b.Child = tb;

				grid.Children.Add(b);

				FancyButton button = new FancyButton();
				button.ImagePath = "reset.png";
				button.Width = 20;
				button.Height = 20;
				button.Visibility = Visibility.Collapsed;
				button.Margin = new Thickness(0, 0, 3, 0);
				button.Click += delegate {
					button.Visibility = Visibility.Collapsed;
					binding.Reset();
					tb.Text = binding.KeyGesture.DisplayString;
					ActEditorConfiguration.Remapper.Remove(actionName);
					b.BorderBrush = WpfUtilities.LostFocusBrush;
				};

				if (binding.CanReset) {
					button.Visibility = Visibility.Visible;
				}

				WpfUtilities.SetGridPosition(button, 0, 1);
				grid.Children.Add(button);

				WpfUtilities.SetGridPosition(grid, row, 1);
				_gridShortcuts.Children.Add(grid);
				_gridShortcuts.RowDefinitions.Add(new RowDefinition { Height = new GridLength(-1, GridUnitType.Auto) });

				tb.GotFocus += delegate {
					b.BorderThickness = new Thickness(2);

					if (b.BorderBrush == Brushes.Red) {
						return;
					}

					b.BorderBrush = WpfUtilities.GotFocusBrush;
				};

				tb.LostFocus += delegate {
					b.BorderThickness = new Thickness(1);

					if (b.BorderBrush == Brushes.Red) {
						return;
					}

					b.BorderBrush = WpfUtilities.LostFocusBrush;
				};

				tb.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
					if (e.Key == Key.Escape || e.Key == Key.Tab) {
						return;
					}

					bool valid;
					tb.Text = _make(e.Key, Keyboard.Modifiers, out valid);

					try {
						if (!valid)
							throw new Exception();

						var b2 = binding;
						var shortcut = ApplicationShortcut.Make(null, e.Key, Keyboard.Modifiers);

						while (b2 != null) {
							b2.Set(shortcut);
							b2 = b2.Next;
						}

						if (binding.CanReset) {
							button.Visibility = Visibility.Visible;
						}
						else {
							button.Visibility = Visibility.Collapsed;
						}

						ActEditorConfiguration.Remapper[actionName] = tb.Text;
						ApplicationShortcut.OverrideBindings(ActEditorConfiguration.Remapper);

						b.BorderThickness = new Thickness(2);
						b.BorderBrush = WpfUtilities.GotFocusBrush;
					}
					catch {
						b.BorderThickness = new Thickness(2);
						b.BorderBrush = Brushes.Red;
						button.Visibility = Visibility.Visible;
					}
					e.Handled = true;
				};

				row++;
			}
		}

		private static string _make(Key key, ModifierKeys modifiers, out bool valid) {
			string display = "";

			if (modifiers.HasFlags(ModifierKeys.Control)) {
				display += "Ctrl-";
			}
			if (modifiers.HasFlags(ModifierKeys.Shift)) {
				display += "Shift-";
			}
			if (modifiers.HasFlags(ModifierKeys.Alt)) {
				display += "Alt-";
			}
			if (modifiers.HasFlags(ModifierKeys.Windows)) {
				display += "Win-";
			}

			if (key == Key.LeftAlt ||
				key == Key.RightAlt ||
				key == Key.LeftCtrl ||
				key == Key.RightCtrl ||
				key == Key.LeftShift ||
				key == Key.RightShift ||
				key == Key.System ||
				key == Key.LWin ||
				key == Key.RWin) {
				valid = false;
				return display;
			}

			valid = true;
			display += key;
			return display;
		}

		private void _fbResetShortcuts_Click(object sender, RoutedEventArgs e) {
			ActEditorConfiguration.Remapper.Clear();
			_gridShortcuts.Children.Clear();
			ApplicationShortcut.ResetBindings();
			ApplicationShortcut.OverrideBindings(ActEditorConfiguration.Remapper);
			_loadShortcuts();
		}

		private void _fbRefreshhortcuts_Click(object sender, RoutedEventArgs e) {
			_gridShortcuts.Children.Clear();
			_loadShortcuts();
		}

		private bool _setEncoding(int encoding) {
			if (EncodingService.SetDisplayEncoding(encoding)) {
				ActEditorConfiguration.EncodingCodepage = encoding;
				return true;
			}

			return false;
		}

		protected override void OnClosing(CancelEventArgs e) {
			_resourceGrfs.Children.Clear();
			base.OnClosing(e);
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _tbSearch_TextChanged(object sender, TextChangedEventArgs e) {
			if (_tbSearch.Text == "") {
				foreach (var key in _shortcuts.Values) {
					key.Grid.Visibility = Visibility.Visible;
					key.Label.Visibility = Visibility.Visible;
				}
			}
			else {
				foreach (var key in _shortcuts) {
					if (key.Key.IndexOf(_tbSearch.Text, StringComparison.OrdinalIgnoreCase) > -1) {
						key.Value.Grid.Visibility = Visibility.Visible;
						key.Value.Label.Visibility = Visibility.Visible;
					}
					else {
						key.Value.Grid.Visibility = Visibility.Collapsed;
						key.Value.Label.Visibility = Visibility.Collapsed;
					}
				}
			}
		}
	}
}