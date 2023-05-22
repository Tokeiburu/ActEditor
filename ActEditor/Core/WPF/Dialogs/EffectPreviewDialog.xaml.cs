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
using ActEditor.Core.WPF.FrameEditor;
using ActEditor.Core.WPF.GenericControls;
using ColorPicker.Sliders;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SoundEditDialog.xaml
	/// </summary>
	public partial class EffectPreviewDialog : TkWindow {
		private readonly Act _act;
		private readonly EffectConfiguration _effectConfiguration;
		public int ActionIndex { get; set; }
		public int StartIndex { get; set; }
		public static bool Displayed { get; set; }

		public EffectPreviewDialog() : base("Effect properties", "advanced.png") {
			InitializeComponent();
			ShowInTaskbar = true;
			ResizeMode = ResizeMode.CanResize;
		}

		public EffectPreviewDialog(Act act, int actionIndex, EffectConfiguration effectConfiguration) : this() {
			_effectConfiguration = effectConfiguration;
			ActionIndex = actionIndex;
			_act = act;
			_rps.Init(_act, ActionIndex);

			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = () => _rps.Act;
			editor.Element = this;
			editor.FrameSelector = _rps.ToActIndexSelector();
			editor.SelectedActionFunc = () => _rps.SelectedAction;
			editor.SelectedFrameFunc = () => _rps.SelectedFrame;

			_rfp.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (editor.Act != null) {
					return new List<DrawingComponent> { new ActDraw(editor.Act, editor) };
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			_rfp.Init(editor);
			Displayed = true;

			Owner = ActEditorWindow.Instance;

			Closing += delegate {
				ActEditorWindow.Instance.Focus();
			};

			foreach (var property in _effectConfiguration.Properties) {
				var effectProperty = property.Value;

				int gridIndex = _gridProperties.RowDefinitions.Count;
				_gridProperties.RowDefinitions.Add(new RowDefinition {
					Height = new GridLength(0, GridUnitType.Auto)
				});

				Label label = new Label { Padding = new Thickness(), Margin = new Thickness(3), Content = effectProperty.Name };
				label.SetValue(Grid.RowProperty, gridIndex);
				bool eventsEnabled = true;
				float range = 1;

				ClickSelectTextBox box = new ClickSelectTextBox();
				box.TextAlignment = TextAlignment.Right;
				box.BorderThickness = new Thickness(0);
				box.VerticalAlignment = VerticalAlignment.Center;
				box.MinWidth = 70;

				Border b = new Border();
				b.Margin = new Thickness(3);
				b.BorderBrush = Brushes.Transparent;
				b.BorderThickness = new Thickness(1);
				b.VerticalAlignment = VerticalAlignment.Center;
				b.CornerRadius = new CornerRadius(1);
				b.Child = box;

				WpfUtilities.AddFocus(box);

				FancyButton buttonReset = new FancyButton();
				buttonReset.Width = 18;
				buttonReset.Height = 18;
				buttonReset.ImagePath = "reset.png";
				buttonReset.Margin = new Thickness(3);

				buttonReset.Click += delegate {
					if (effectProperty.Type == typeof(GrfColor)) {
						box.Text = ((GrfColor)effectProperty.DefValue).ToHexString();
					}
					else {
						box.Text = effectProperty.DefValue.ToString();
					}

					buttonReset.Visibility = Visibility.Hidden;
				};

				buttonReset.Visibility = Visibility.Hidden;
				buttonReset.SetValue(Grid.ColumnProperty, 3);
				buttonReset.SetValue(Grid.RowProperty, gridIndex);

				if (effectProperty.Type == typeof(float)) {
					SliderColor slider = new SliderColor();
					slider.Width = 100;
					slider.Margin = new Thickness(2);
					slider.HorizontalAlignment = HorizontalAlignment.Left;
					slider.SetValue(Grid.ColumnProperty, 1);
					slider.SetValue(Grid.RowProperty, gridIndex);

					range = (float)effectProperty.MaxValue - (float)effectProperty.MinValue;

					box.Text = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, ((float)effectProperty.DefValue).ToString(CultureInfo.InvariantCulture)];
					slider.SetPosition((FormatConverters.SingleConverter(box.Text) - (float)effectProperty.MinValue) / range, false);

					box.TextChanged += delegate {
						try {
							eventsEnabled = false;
							float f = FormatConverters.SingleConverter(box.Text);

							effectProperty.Value = f;
							ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = f.ToString(CultureInfo.InvariantCulture);
							buttonReset.Visibility = Visibility.Visible;
							slider.SetPosition((f - (float)effectProperty.MinValue) / range, false);

							_updatePreview();
						}
						catch { }
						finally {
							eventsEnabled = true;
						}
					};

					slider.ValueChanged += delegate {
						if (!eventsEnabled) return;
						var val = ((float)(slider.Position * range + (float)effectProperty.MinValue));
						var mod = range / 100f;
						var rMod = 1 / mod;
						val = ((int)(val * rMod)) / rMod;
						slider.SetPosition((val - (float)effectProperty.MinValue) / range, true);
						box.Text = val.ToString(CultureInfo.InvariantCulture);
					};

					buttonReset.Visibility = box.Text != ((float)effectProperty.DefValue).ToString(CultureInfo.InvariantCulture) ? Visibility.Visible : Visibility.Hidden;

					_gridProperties.Children.Add(slider);
				}
				else if (effectProperty.Type == typeof(int)) {
					SliderColor slider = new SliderColor();
					slider.Width = 100;
					slider.Margin = new Thickness(2);
					slider.HorizontalAlignment = HorizontalAlignment.Left;
					slider.SetValue(Grid.ColumnProperty, 1);
					slider.SetValue(Grid.RowProperty, gridIndex);

					range = (int)effectProperty.MaxValue - (int)effectProperty.MinValue;

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

							_updatePreview();
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
				else if (effectProperty.Type == typeof(string)) {
					if (effectProperty.MinValue != null && (string)effectProperty.MinValue == "FileSelect") {
						ComboBox cBox = new ComboBox();
						cBox.Margin = new Thickness(3);
						cBox.SetValue(Grid.RowProperty, gridIndex);
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
								eventsEnabled = false;

								if (cBox.SelectedItem != null) {
									effectProperty.Value = ((ComboBoxItem)cBox.SelectedItem).Tag.ToString();
								}

								_updatePreview();
							}
							catch { }
							finally {
								eventsEnabled = true;
							}
						};

						if (cBox.SelectedItem != null) {
							effectProperty.Value = ((ComboBoxItem)cBox.SelectedItem).Tag.ToString();
						}

						b.Visibility = Visibility.Collapsed;
						_gridProperties.Children.Add(cBox);
					}
					else {
						box.Text = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, effectProperty.DefValue.ToString()];

						box.TextChanged += delegate {
							try {
								eventsEnabled = false;
								effectProperty.Value = box.Text;
								ActEditorConfiguration.ConfigAsker[effectProperty.SettingName] = box.Text;
								buttonReset.Visibility = Visibility.Visible;
								_updatePreview();
							}
							catch { }
							finally {
								eventsEnabled = true;
							}
						};

						buttonReset.Visibility = box.Text != effectProperty.DefValue.ToString() ? Visibility.Visible : Visibility.Hidden;
					}
				}
				else if (effectProperty.Type == typeof(GrfColor)) {
					var colorSetting = ActEditorConfiguration.ConfigAsker[effectProperty.SettingName, ((GrfColor)effectProperty.DefValue).ToHexString()];

					QuickColorSelector qcs = new QuickColorSelector();
					qcs.Width = 100;
					qcs.Margin = new Thickness(2);
					qcs.SetValue(Grid.ColumnProperty, 1);
					qcs.SetValue(Grid.RowProperty, gridIndex);
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
							_updatePreview();
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

				b.SetValue(Grid.ColumnProperty, 2);
				b.SetValue(Grid.RowProperty, gridIndex);

				_gridProperties.Children.Add(label);
				_gridProperties.Children.Add(b);
				_gridProperties.Children.Add(buttonReset);
			}

			_updatePreview();

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

		private void _updatePreview() {
			if (_act == null) return;

			LazyAction.Execute(delegate {
				Act act = new Act(new Spr(_act.Sprite));
				
				foreach (var action in _act) {
					act.AddAction(new Action(action));
				}

				try {
					Execute(act, true);
					this.BeginDispatch(() => _rps.SelectedFrame = StartIndex);
					_rps.Play();
				}
				catch (Exception err) {
					_rps.Stop();
					ErrorHandler.HandleException(err);
				}

				_rps.Init(act, ActionIndex);
			}, GetHashCode());
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
				_rps.Stop();
				Displayed = false;
			}
		}

		public void Execute(Act act, bool executeCommands = true) {
			act.Commands.BeginNoDelay();

			try {
				_effectConfiguration.EffectFunc(act);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				act.Commands.End();
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
			Execute(_act, true);
			DialogResult = true;
			Close();
		}
	}
}