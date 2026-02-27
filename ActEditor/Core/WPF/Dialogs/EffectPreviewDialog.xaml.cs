using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.Scripting;
using ActEditor.Core.WPF.FrameEditor;
using ActEditor.Core.WPF.GenericControls;
using ActEditor.Core.WPF.InteractionComponent;
using ColorPicker.Sliders;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SoundEditDialog.xaml
	/// </summary>
	public partial class EffectPreviewDialog : TkWindow {
		private Act _act;
		private EffectConfiguration _effectConfiguration;
		private Act _rendererAct;
		private DummyFrameEditor _editor;
		public int ActionIndex { get; set; }
		public int StartIndex { get; set; }
		public static bool Displayed { get; set; } = true;

		private IActIndexSelector _selector {
			get { return _effectConfiguration.ActIndexSelectorReadonly ? (IActIndexSelector)_actIndexReadonly : _actIndexSelector; }
		}

		public EffectPreviewDialog() : base("Effect properties", "advanced.png") {
			InitializeComponent();
			ShowInTaskbar = true;
			ResizeMode = ResizeMode.CanResize;
			UseLayoutRounding = true;
			SnapsToDevicePixels = true;
		}

		public class RowCreationData {
			public FancyButton ButtonReset;
			public ClickSelectTextBox TextEditBox;
			public Border TextEditBorder;
			public TextBlock DisplayLabel;
			public int CurrentRowIndex;
			public Grid GridProperties;
			public EffectConfiguration.EffectProperty EffectProperty;
			public System.Action Update;
		}

		public EffectPreviewDialog(Act act, int actionIndex, EffectConfiguration effectConfiguration) : this() {
			Title += " - " + effectConfiguration.ParentType;
			_effectConfiguration = effectConfiguration;

			if (effectConfiguration.PreferredSelectedAction > -1 && effectConfiguration.PreferredSelectedAction < act.NumberOfActions)
				actionIndex = effectConfiguration.PreferredSelectedAction;

			_initializeEditor(act, actionIndex);
			_buildPropertyUI();
			_initializePreview();
		}

		private void _initializePreview() {
			_requestPreviewUpdate();

			if (EffectConfiguration.SkipAndRememberInput > 0) {
				Loaded += delegate {
					if (EffectConfiguration.SkipAndRememberInput == 1) {
						EffectConfiguration.SkipAndRememberInput = 2;
					}
					else {
						Execute(_act);
						DialogResult = true;
					}
				};
			}
		}

		private void _initializeEditor(Act act, int actionIndex) {
			ActionIndex = actionIndex;
			_act = act;
			_rendererAct = new Act(act);

			((UIElement)_selector).Visibility = Visibility.Visible;

			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = () => _rendererAct;
			editor.Element = this;
			editor.IndexSelector = _selector;
			editor.SelectedActionFunc = () => _selector.SelectedAction;
			editor.SelectedFrameFunc = () => _selector.SelectedFrame;
			editor.FrameRenderer = _rfp;
			_editor = editor;

			_loadSelector();
			_selector.SelectedAction = actionIndex;

			_rfp.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (editor.Act != null) {
					return new List<DrawingComponent> { new ActDraw(editor.Act, editor) };
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			_rfp.Init(editor);

			_cbAutoPlay.IsChecked = _effectConfiguration.AutoPlay;
			_cbAutoPlay.Checked += delegate {
				_effectConfiguration.AutoPlay = true;
				_editor.IndexSelector.Play();
			};
			_cbAutoPlay.Unchecked += delegate {
				_effectConfiguration.AutoPlay = false;
				_editor.IndexSelector.Stop();
			};
			WpfUtilities.AddMouseInOutUnderline(_cbAutoPlay);

			Owner = ActEditorWindow.Instance;

			Closing += delegate {
				ActEditorWindow.Instance.Focus();
			};
		}

		#region UI preview build

		private void _buildPropertyUI() {
			foreach (var property in _effectConfiguration.Properties) {
				var effectProperty = property.Value;

				RowCreationData rowData = new RowCreationData();

				rowData.EffectProperty = effectProperty;
				rowData.CurrentRowIndex = _gridProperties.RowDefinitions.Count;
				rowData.GridProperties = _gridProperties;
				rowData.Update = _requestPreviewUpdate;

				AddNewGridRow();

				if (effectProperty.DefValue is ICustomPreviewProperty) {
					((ICustomPreviewProperty)effectProperty.Value).CreateTemplate(rowData);
				}
				else {
					rowData.DisplayLabel = CreateDisplayLabel(rowData);
					rowData.TextEditBox = CreateTextEditBox(rowData);
					rowData.TextEditBorder = CreateTextEditBorder(rowData);
					rowData.ButtonReset = CreateButtonReset(rowData);
				}

				if (effectProperty.Type == typeof(float)) {
					CreateFloatProperty(rowData);
				}
				else if (effectProperty.Type == typeof(int)) {
					CreateIntProperty(rowData);
				}
				else if (effectProperty.Type == typeof(string)) {
					CreateStringProperty(rowData);
				}
				else if (effectProperty.Type == typeof(GrfColor)) {
					CreateColorProperty(rowData);
				}
				else if (effectProperty.Type == typeof(bool)) {
					CreateBooleanProperty(rowData);
				}
				else if (effectProperty.Type == typeof(TkVector2)) {
					CreateTkVectorProperty(rowData);
				}

				if (!(effectProperty.DefValue is ICustomPreviewProperty)) {
					_gridProperties.Children.Add(rowData.DisplayLabel);
					_gridProperties.Children.Add(rowData.TextEditBorder);
					_gridProperties.Children.Add(rowData.ButtonReset);
				}
			}
		}

		private void CreateColorProperty(RowCreationData rowData) {
			var effectProperty = rowData.EffectProperty;
			bool eventsEnabled = true;
			var box = rowData.TextEditBox;
			var buttonReset = rowData.ButtonReset;

			var colorSetting = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, ((GrfColor)effectProperty.DefValue).ToHexString()];

			QuickColorSelector qcs = new QuickColorSelector();
			qcs.HorizontalAlignment = HorizontalAlignment.Stretch;
			qcs.Margin = new Thickness(5);
			qcs.SetValue(Grid.ColumnProperty, 1);
			qcs.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);
			qcs.Color = new GrfColor(colorSetting).ToColor();
			qcs.PreviewUpdateInterval = 100;

			box.Text = new GrfColor(colorSetting).ToHexString();

			if (box.Text != ((GrfColor)effectProperty.DefValue).ToHexString())
				buttonReset.Visibility = Visibility.Visible;

			box.TextChanged += delegate {
				try {
					eventsEnabled = false;

					var f = new GrfColor(box.Text);

					effectProperty.Value = f;
					qcs.Color = f.ToColor();
					ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = f.ToHexString();
					buttonReset.Visibility = Visibility.Visible;
					_requestPreviewUpdate();
				}
				catch {
				}
				finally {
					eventsEnabled = true;
				}
			};

			qcs.PreviewColorChanged += (sender, color) => {
				if (!eventsEnabled) return;
				box.Text = color.ToGrfColor().ToHexString();
			};

			qcs.ColorChanged += (sender, color) => {
				if (!eventsEnabled) return;
				box.Text = color.ToGrfColor().ToHexString();
			};

			_gridProperties.Children.Add(qcs);
		}

		private void CreateBooleanProperty(RowCreationData rowData) {
			var effectProperty = rowData.EffectProperty;
			var box = rowData.TextEditBox;
			var buttonReset = rowData.ButtonReset;

			var colorSetting = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, ((bool)effectProperty.DefValue).ToString()];

			CheckBox cb = new CheckBox();
			cb.Margin = new Thickness(3);
			cb.SetValue(Grid.ColumnProperty, 1);
			cb.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);
			cb.IsChecked = Boolean.Parse(colorSetting);
			cb.HorizontalAlignment = HorizontalAlignment.Center;
			cb.VerticalAlignment = VerticalAlignment.Center;

			buttonReset.Visibility = Visibility.Collapsed;
			rowData.TextEditBorder.Visibility = Visibility.Collapsed;
			box.Visibility = Visibility.Collapsed;

			cb.Checked += delegate {
				effectProperty.Value = true;
				ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = true.ToString();
				_requestPreviewUpdate();
			};

			cb.Unchecked += delegate {
				effectProperty.Value = false;
				ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = false.ToString();
				_requestPreviewUpdate();
			};

			_gridProperties.Children.Add(cb);
		}

		private void CreateTkVectorProperty(RowCreationData rowData) {
			var effectProperty = rowData.EffectProperty;
			var box = rowData.TextEditBox;
			var buttonReset = rowData.ButtonReset;

			DockPanel panel = new DockPanel();

			IntTextBoxEdit tbX = new IntTextBoxEdit();
			tbX.TextNoEvent = ((TkVector2)rowData.EffectProperty.Value).X.ToString("0.##");
			tbX.Margin = new Thickness(1);
			tbX.Width = 90;
			tbX.MinValue = (int)((TkVector2)rowData.EffectProperty.MinValue).X;
			tbX.MaxValue = (int)((TkVector2)rowData.EffectProperty.MinValue).Y;

			IntTextBoxEdit tbY = new IntTextBoxEdit();
			tbY.TextNoEvent = ((TkVector2)rowData.EffectProperty.Value).Y.ToString("0.##");
			tbY.Margin = new Thickness(1);
			tbY.Width = 90;
			tbY.MinValue = (int)((TkVector2)rowData.EffectProperty.MaxValue).X;
			tbY.MaxValue = (int)((TkVector2)rowData.EffectProperty.MaxValue).Y;

			panel.SetValue(Grid.ColumnProperty, 1);
			panel.SetValue(Grid.ColumnSpanProperty, 2);
			panel.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);

			TextBlock tX = new TextBlock();
			tX.Text = "X = ";
			tX.VerticalAlignment = VerticalAlignment.Center;
			panel.Children.Add(tX);
			panel.Children.Add(tbX);

			TextBlock tY = new TextBlock();
			tY.Text = "Y = ";
			tY.VerticalAlignment = VerticalAlignment.Center;
			tY.Margin = new Thickness(5, 0, 0, 0);
			panel.Children.Add(tY);
			panel.Children.Add(tbY);

			buttonReset.Click += delegate {
				tbX.Text = ((TkVector2)rowData.EffectProperty.DefValue).X.ToString("0.##");
				tbY.Text = ((TkVector2)rowData.EffectProperty.DefValue).Y.ToString("0.##");
			};

			buttonReset.Visibility = Visibility.Collapsed;
			rowData.TextEditBorder.Visibility = Visibility.Collapsed;
			box.Visibility = Visibility.Collapsed;

			tbX.TextChanged += delegate {
				effectProperty.Value = new TkVector2(FormatConverters.SingleConverterNoThrow(tbX.Text), FormatConverters.SingleConverterNoThrow(tbY.Text));
				ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = effectProperty.Value.ToString();
				_requestPreviewUpdate();
			};

			tbY.TextChanged += delegate {
				effectProperty.Value = new TkVector2(FormatConverters.SingleConverterNoThrow(tbX.Text), FormatConverters.SingleConverterNoThrow(tbY.Text));
				ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = effectProperty.Value.ToString();
				_requestPreviewUpdate();
			};

			_gridProperties.Children.Add(panel);
		}

		private void CreateStringProperty(RowCreationData rowData) {
			var effectProperty = rowData.EffectProperty;
			var box = rowData.TextEditBox;
			var buttonReset = rowData.ButtonReset;

			if (effectProperty.MinValue != null && (string)effectProperty.MinValue == "FileSelect") {
				ComboBox cBox = new ComboBox();
				cBox.Margin = new Thickness(3);
				cBox.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);
				cBox.SetValue(Grid.ColumnProperty, 2);
				cBox.SetValue(Grid.ColumnSpanProperty, 1);
				cBox.MaxWidth = 120;
				cBox.HorizontalAlignment = HorizontalAlignment.Left;
				var path = GrfPath.Combine(Configuration.ProgramDataPath, ScriptLoader.OutputPath);

				foreach (var file in Directory.GetFiles(path, (string)effectProperty.DefValue)) {
					cBox.Items.Add(new ComboBoxItem { Content = Path.GetFileNameWithoutExtension(file), Tag = file });
				}

				cBox.SelectedIndex = 0;
				cBox.SelectionChanged += delegate {
					try {
						if (cBox.SelectedItem != null) {
							effectProperty.Value = ((ComboBoxItem)cBox.SelectedItem).Tag.ToString();
						}

						_requestPreviewUpdate();
					}
					catch { }
				};

				if (cBox.SelectedItem != null) {
					effectProperty.Value = ((ComboBoxItem)cBox.SelectedItem).Tag.ToString();
				}

				rowData.TextEditBorder.Visibility = Visibility.Collapsed;
				_gridProperties.Children.Add(cBox);
			}
			else {
				box.Text = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, effectProperty.DefValue.ToString()];

				box.TextChanged += delegate {
					try {
						effectProperty.Value = box.Text;
						ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = box.Text;
						buttonReset.Visibility = Visibility.Visible;
						_requestPreviewUpdate();
					}
					catch { }
				};

				buttonReset.Visibility = box.Text != effectProperty.DefValue.ToString() ? Visibility.Visible : Visibility.Hidden;
			}
		}

		private void CreateIntProperty(RowCreationData rowData) {
			var effectProperty = rowData.EffectProperty;
			float range = (int)effectProperty.MaxValue - (int)effectProperty.MinValue;
			bool eventsEnabled = true;
			var box = rowData.TextEditBox;
			var buttonReset = rowData.ButtonReset;

			SliderColor slider = new SliderColor();
			slider.HorizontalAlignment = HorizontalAlignment.Stretch;
			slider.Margin = new Thickness(2);
			slider.SetValue(Grid.ColumnProperty, 1);
			slider.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);

			box.Text = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, effectProperty.DefValue.ToString()];
			slider.SetPosition((FormatConverters.IntConverter(box.Text) - (int)effectProperty.MinValue) / range, true);

			box.TextChanged += delegate {
				try {
					eventsEnabled = false;
					float f = FormatConverters.IntConverter(box.Text);

					effectProperty.Value = (int)f;
					ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = ((int)f).ToString(CultureInfo.InvariantCulture);
					buttonReset.Visibility = Visibility.Visible;
					slider.SetPosition((f - (int)effectProperty.MinValue) / range, false);

					_requestPreviewUpdate();
				}
				catch { }
				finally {
					eventsEnabled = true;
				}
			};

			slider.ValueChanged += delegate {
				if (!eventsEnabled) return;
				var val = (int)Math.Round(slider.Position * range + (int)effectProperty.MinValue, MidpointRounding.AwayFromZero);
				slider.SetPosition((val - (int)effectProperty.MinValue) / range, true);
				box.Text = val.ToString(CultureInfo.InvariantCulture);
			};

			buttonReset.Visibility = box.Text != effectProperty.DefValue.ToString() ? Visibility.Visible : Visibility.Hidden;

			_gridProperties.Children.Add(slider);
		}

		private FancyButton CreateButtonReset(RowCreationData rowData) {
			FancyButton buttonReset = new FancyButton();
			buttonReset.Width = 18;
			buttonReset.Height = 18;
			buttonReset.ImagePath = "reset.png";
			buttonReset.Margin = new Thickness(3);

			buttonReset.Click += delegate {
				if (rowData.EffectProperty.Type == typeof(bool)) {
					rowData.EffectProperty.Value = (bool)rowData.EffectProperty.DefValue;
				}
				else if (rowData.EffectProperty.Type == typeof(GrfColor)) {
					rowData.TextEditBox.Text = ((GrfColor)rowData.EffectProperty.DefValue).ToHexString();
				}
				else if (rowData.EffectProperty.Type == typeof(TkVector2)) {
					
				}
				else {
					rowData.TextEditBox.Text = rowData.EffectProperty.DefValue.ToString();
				}

				buttonReset.Visibility = Visibility.Hidden;
			};

			buttonReset.Visibility = Visibility.Hidden;
			buttonReset.SetValue(Grid.ColumnProperty, 3);
			buttonReset.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);
			return buttonReset;
		}

		private Border CreateTextEditBorder(RowCreationData rowData) {
			Border b = new Border();
			b.Margin = new Thickness(3);
			b.BorderBrush = Brushes.Transparent;
			b.BorderThickness = new Thickness(1);
			b.VerticalAlignment = VerticalAlignment.Center;
			b.CornerRadius = new CornerRadius(1);
			b.Child = rowData.TextEditBox;

			b.SetValue(Grid.ColumnProperty, 2);
			b.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);

			WpfUtilities.AddFocus(rowData.TextEditBox);
			return b;
		}

		private ClickSelectTextBox CreateTextEditBox(RowCreationData rowData) {
			ClickSelectTextBox box = new ClickSelectTextBox();
			box.TextAlignment = TextAlignment.Right;
			box.BorderThickness = new Thickness(0);
			box.VerticalAlignment = VerticalAlignment.Center;
			box.MinWidth = 70;
			return box;
		}

		private TextBlock CreateDisplayLabel(RowCreationData rowData) {
			TextBlock label = new TextBlock { Padding = new Thickness(), Margin = new Thickness(3), Text = rowData.EffectProperty.Name.ToString(), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
			label.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);

			if (!String.IsNullOrEmpty(rowData.EffectProperty.ToolTip)) {
				label.ToolTip = rowData.EffectProperty.ToolTip;
				label.SetValue(ToolTipService.InitialShowDelayProperty, 100);
				WpfUtilities.AddMouseInOutUnderline(label);
			}

			return label;
		}

		private void CreateFloatProperty(RowCreationData rowData) {
			SliderColor slider = new SliderColor();
			slider.HorizontalAlignment = HorizontalAlignment.Stretch;
			slider.Margin = new Thickness(2);
			slider.SetValue(Grid.ColumnProperty, 1);
			slider.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);

			float range = (float)rowData.EffectProperty.MaxValue - (float)rowData.EffectProperty.MinValue;

			rowData.TextEditBox.Text = ActEditorConfiguration.ConfigAsker[rowData.EffectProperty.SettingName, ((float)rowData.EffectProperty.DefValue).ToString(CultureInfo.InvariantCulture)];
			slider.SetPosition((FormatConverters.SingleConverter(rowData.TextEditBox.Text) - (float)rowData.EffectProperty.MinValue) / range, false);

			bool eventsEnabled = true;

			rowData.TextEditBox.TextChanged += delegate {
				try {
					eventsEnabled = false;
					float f = FormatConverters.SingleConverter(rowData.TextEditBox.Text);

					rowData.EffectProperty.Value = f;
					ActEditorConfiguration.ConfigAsker[rowData.EffectProperty.SettingName] = f.ToString(CultureInfo.InvariantCulture);
					rowData.ButtonReset.Visibility = Visibility.Visible;
					slider.SetPosition((f - (float)rowData.EffectProperty.MinValue) / range, false);

					_requestPreviewUpdate();
				}
				catch { }
				finally {
					eventsEnabled = true;
				}
			};

			slider.ValueChanged += delegate {
				if (!eventsEnabled) return;
				var val = ((float)(slider.Position * range + (float)rowData.EffectProperty.MinValue));
				var mod = range / 100f;
				var rMod = 1 / mod;
				val = ((int)(val * rMod)) / rMod;
				slider.SetPosition((val - (float)rowData.EffectProperty.MinValue) / range, true);
				rowData.TextEditBox.Text = val.ToString(CultureInfo.InvariantCulture);
			};

			rowData.ButtonReset.Visibility = rowData.TextEditBox.Text != ((float)rowData.EffectProperty.DefValue).ToString(CultureInfo.InvariantCulture) ? Visibility.Visible : Visibility.Hidden;

			_gridProperties.Children.Add(slider);
		}

		private void AddNewGridRow() {
			_gridProperties.RowDefinitions.Add(new RowDefinition {
				Height = new GridLength(0, GridUnitType.Auto)
			});
		}

		#endregion

		private void _loadSelector() {
			_selector.Init(_editor, -1, -1);
		}

		private CoalescingExecutor _executor = new CoalescingExecutor();

		private void _requestPreviewUpdate() {
			if (_act == null) return;

			_executor.Execute(delegate {
				Act act = new Act(_act);

				int oldSelectedFrame = _selector.SelectedFrame;

				try {
					var oldFrameCount = act[_selector.SelectedAction].NumberOfFrames;

					Execute(act);

					//_selector.SelectedFrame = StartIndex;

					if (_effectConfiguration.AutoPlay) {
						_selector.SelectedFrame = StartIndex;
						_selector.Play();
					}
				}
				catch (Exception err) {
					_selector.Stop();
					ErrorHandler.HandleException(err);
				}

				_rendererAct = act;

				this.Dispatch(delegate {
					_loadSelector();

					if (!_effectConfiguration.AutoPlay)
						_selector.SelectedFrame = oldSelectedFrame;
				});
			});
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				Execute(_act);
				DialogResult = true;
			}
			base.GRFEditorWindowKeyDown(sender, e);
		}

		protected override void OnClosing(CancelEventArgs e) {
			if (!e.Cancel) {
				_selector.Stop();
				Displayed = false;
			}
		}

		public void Execute(Act act) {
			try {
				act.Commands.ActEditBegin("Effect: " + _effectConfiguration.ParentType);
				_effectConfiguration.EffectFunc(act);
			}
			catch (Exception err) {
				act.Commands.ActCancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				act.Commands.ActEditEnd();
				act.InvalidateVisual();

				if (_effectConfiguration.InvalidateSprite) {
					act.InvalidateSpriteVisual();
				}
			}
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _buttonApply_Click(object sender, RoutedEventArgs e) {
			Execute(_act);
			DialogResult = true;
			Close();
		}
	}

	public interface ICustomPreviewProperty {
		void CreateTemplate(EffectPreviewDialog.RowCreationData rowData);
	}
}