using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorPicker;
using ColorPicker.Core.Commands;
using ColorPicker.Sliders;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Utilities.Commands;
using Utilities.Controls;
using ICommand = ColorPicker.Core.Commands.ICommand;

namespace PaletteEditor {
	/// <summary>
	/// Interaction logic for SingleColorEditControl.xaml
	/// </summary>
	public partial class SingleColorEditControl : UserControl {
		private byte[] _oldColor = new byte[4];
		private Pal _pal;

		public bool PalAtTop {
			get { return (bool)GetValue(PalAtTopProperty); }
			set { SetValue(PalAtTopProperty, value); }
		}
		public static DependencyProperty PalAtTopProperty = DependencyProperty.Register("PalAtTop", typeof(bool), typeof(SingleColorEditControl), new PropertyMetadata(new PropertyChangedCallback(OnPalAtTopChanged)));
		private bool _assigned;

		private static void OnPalAtTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var obj = d as SingleColorEditControl;

			if (obj != null) {
				if (Boolean.Parse(e.NewValue.ToString())) {
					obj._paletteSelector.SetValue(Grid.RowProperty, 0);
					obj._paletteSelector.SetValue(Grid.ColumnProperty, 1);
					//obj._paletteSelector.HorizontalAlignment = HorizontalAlignment.Right;
				}
				else {
					obj._paletteSelector.SetValue(Grid.RowProperty, 1);
					obj._paletteSelector.SetValue(Grid.ColumnProperty, 0);
					//obj._paletteSelector.HorizontalAlignment = HorizontalAlignment.Left;
				}
			}
		}

		public SingleColorEditControl() {
			InitializeComponent();

			_colorPicker.ColorChanged += new SliderGradient.GradientPickerColorEventHandler(_colorPicker_ColorChanged);
			_colorPicker.Commands.PreviewCommandExecuted += new AbstractCommand<ICommand>.AbstractCommandsEventHandler(_commands_PreviewCommandExecuted);
			_paletteSelector.IsMultipleColorsSelectable = true;
			_paletteSelector.SelectionChanged += new ObservableList.ObservableListEventHandler(_paletteSelector_SelectionChanged);
			_colorPicker.PreviewColorChanged += new SliderGradient.GradientPickerColorEventHandler(_colorPicker_PreviewColorChanged);
			Loaded += delegate {
				Window parent = WpfUtilities.FindParentControl<Window>(this);

				if (parent != null)
					parent.PreviewKeyDown += new KeyEventHandler(_sce_PreviewKeyDown);
			};
		}

		public PaletteSelector PaletteSelector {
			get { return _paletteSelector; }
		}

		public PickerControl PickerControl {
			get { return _colorPicker; }
		}

		private void _colorPicker_PreviewColorChanged(object sender, Color value) {
			if (_paletteSelector.SelectedItems.Count == 0) return;

			int baseIndex = _paletteSelector.SelectedItems.Last();

			_oldColor = new byte[4];
			Buffer.BlockCopy(_pal.BytePalette, baseIndex * 4, _oldColor, 0, _oldColor.Length);
		}

		public void SetPalette(Pal pal) {
			_pal = pal;
			_paletteSelector.SetPalette(pal);
			pal.Commands.CommandRedo += _palUpdate;
			pal.Commands.CommandUndo += _palUpdate;

			if (!_assigned) {
				_paletteSelector.PaletteSelectorPaletteChanged += sender => _palUpdate(sender, null);
				_assigned = true;
			}
		}

		private void _palUpdate(object sender, IPaletteCommand command) {
			if (_paletteSelector.SelectedItems.Count > 0)
				_paletteSelector_SelectionChanged(this, new ObservabableListEventArgs(new List<object>(), ObservableListEventType.Added));
		}

		private void _paletteSelector_SelectionChanged(object sender, ObservabableListEventArgs args) {
			if (args.Action == ObservableListEventType.Added) {
				if (_paletteSelector.SelectedItem != null)
					_colorPicker.SelectColorNoEvents(_pal.GetColor(_paletteSelector.SelectedItem.Value).ToColor());
			}
		}

		private void _colorPicker_ColorChanged(object sender, Color value) {
			if (_paletteSelector.SelectedItem != null) {
				int index = _paletteSelector.SelectedItem.Value;
				_pal.SetBytes(index * 4, value.ToBytesRgba());
				//_oldColor = value.ToBytesRgba();
			}
		}

		private void _commands_PreviewCommandExecuted(object sender, ICommand command) {
			ChangeColor changeColor = (ChangeColor) command;

			byte[] gradient = changeColor.NewColor.ToBytesRgba();

			if (gradient != null) {
				if (_paletteSelector.SelectedItems.Count == 0) return;

				int baseIndex = _paletteSelector.SelectedItems.Last();
				_pal.EnableRaiseEvents = false;
				_pal.Commands.ChangeColor(baseIndex, _oldColor, gradient);
				_pal.EnableRaiseEvents = true;
			}
		}

		private void _sce_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if (_pal.Commands.CanUndo) {
					_pal.Commands.Undo();

					if (_paletteSelector.SelectedItem != null) {
						GrfColor color = _pal.GetColor(_paletteSelector.SelectedItem.Value);
						_colorPicker.SelectColorNoEvents(color.ToColor());
					}
				}
				e.Handled = true;
			}

			if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if (_pal.Commands.CanRedo) {
					_pal.Commands.Redo();

					if (_paletteSelector.SelectedItem != null) {
						GrfColor color = _pal.GetColor(_paletteSelector.SelectedItem.Value);
						_colorPicker.SelectColorNoEvents(color.ToColor());
					}
				}
				e.Handled = true;
			}
		}

		public void FocusGrid() {
			PaletteSelector.GridFocus.Focus();
			Keyboard.Focus(PaletteSelector.GridFocus);
		}
	}
}