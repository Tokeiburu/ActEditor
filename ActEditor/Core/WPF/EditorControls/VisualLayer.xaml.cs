using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.FrameEditor;
using ActEditor.Core.WPF.GenericControls;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for SubFrameControl.xaml
	/// The layer control is the view model of the Layer structure.
	/// This class has the following optimizations : 
	/// - The ClickSelectTextBoxes are replaced by TextBlocks when animations are playing
	/// - The control is only rendered and updated if it's visible
	/// - The object is reusable; it isn't bound to any actual layer
	/// - Some commands are auto-reversible (see the AbstractCommand object)
	/// - Brushes are buffered
	/// 
	/// </summary>
	public partial class VisualLayer : UserControl {
		private static Brush _brushSelected;
		private static Brush _brushHovered;
		private static Brush _brushBackground;

		static VisualLayer() {
			ApplicationManager.ThemeChanged += delegate {
				_updateBrushes();
			};

			_updateBrushes();
		}

		private static void _updateBrushes() {
			_brushSelected = (Brush)Application.Current.Resources["UIThemeLayerControlSelectedBrush"];
			_brushHovered = (Brush)Application.Current.Resources["UIThemeLayerControlPreviewSelectedBrush"];
			_brushBackground = (Brush)Application.Current.Resources["UIThemeTextBoxBackgroundBrush"];
		}

		#region Delegates

		public delegate void LayerPropertyChangedDelegate(object sender);

		#endregion

		public static double ActualHeightBuffered;

		private readonly FrameRenderer _renderer;

		private Act _act;
		private int _actionIndex;

		private bool _eventsEnabled = true;
		private int _frameIndex;
		private bool _isPreviewSelected;
		private bool _lastSelected;
		private int _layerIndex;
		private TabAct _actEditor;
		public int LayerIndex => _layerIndex;
		public bool IsVisualLayer { get; set; }
		public bool IsVisualDirty { get; set; } = true;

		private TextBlock[] _boxes;

		public class CachedLayerData {
			public int LayerIndex = int.MaxValue;
			public int SpriteNumber = int.MaxValue;
			public bool? Mirror;
			public int OffsetX = int.MaxValue;
			public int OffsetY = int.MaxValue;
			public float ScaleX = float.MaxValue;
			public float ScaleY = float.MaxValue;
			public int Rotation = int.MaxValue;
		}

		private CachedLayerData _currentLayerValues = new CachedLayerData();

		public VisualLayer() {
			InitializeComponent();
		}

		public VisualLayer(Act act, TabAct actEditor, int layerIndex) {
			_act = act;
			_renderer = (FrameRenderer)actEditor.FrameRenderer;
			_actEditor = actEditor;

			InitializeComponent();

			_commonInit();
			SetLayerIndex(layerIndex);
			_initEvents();

			_boxes = new[] { _tbLayerId, _tbSpriteNumber, _tbOffsetX, _tbOffsetY, null, null, _tbScaleX, _tbScaleY, _tbRotation };
		}

		public void SetLayerIndex(int value) {
			if (_currentLayerValues.LayerIndex == value)
				return;

			_layerIndex = value;
			_currentLayerValues.LayerIndex = value;
			_tbLayerId.Text = value.ToString(CultureInfo.InvariantCulture);
		}

		public void SetMirror(bool value) {
			if (_currentLayerValues.Mirror == value)
				return;

			_currentLayerValues.Mirror = value;
			_cbMirror.IsChecked = value;
		}

		/// <summary>
		/// Attempts to get the current layer (shortchut).
		/// </summary>
		private Layer _layer {
			get {
				if (IsVisualLayer)
					return _act.TryGetLayer(_actEditor.SelectedAction, _actEditor.SelectedFrame, _layerIndex);

				return _act.TryGetLayer(_actionIndex, _frameIndex, _layerIndex);
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this control is preview selected (mouse over).
		/// </summary>
		public bool IsPreviewSelected {
			get { return _isPreviewSelected; }
			set {
				if (_isPreviewSelected == value) return;

				_isPreviewSelected = value;

				if (_lastSelected) return;

				_grid.Background = _isPreviewSelected ? _brushHovered : _brushBackground;
			}
		}

		public event LayerPropertyChangedDelegate LayerPropertyChanged;

		public void OnLayerPropertyChanged() {
			LayerPropertyChangedDelegate handler = LayerPropertyChanged;
			if (handler != null) handler(this);
		}

		public void DrawSelection() {
			if (_actEditor == null)
				return;

			bool isSelected = _actEditor.SelectionEngine.IsSelected(_layerIndex);

			if (isSelected == _lastSelected)
				return;

			_lastSelected = isSelected;
			_grid.Background = _lastSelected ? _brushSelected : _brushBackground;
		}

		private static bool _getDecimalVal(string text, out float dval) {
			if (float.TryParse(text, out dval)) {
				return true;
			}

			text = text.Replace(",", ".");

			if (float.TryParse(text, out dval)) {
				return true;
			}

			text = text.Replace(".", ",");

			if (float.TryParse(text, out dval)) {
				return true;
			}

			return false;
		}

		/// <summary>
		/// Init method shared by all constructor
		/// </summary>
		private void _commonInit() {
			IsEnabledChanged += delegate { _color.IsEnabled = IsEnabled; };
		}

		/// <summary>
		/// Initializes the events
		/// </summary>
		private void _initEvents() {
			_tbLayerId.MouseUp += _tbSpriteId_MouseUp;
			_cbMirror.Checked += _cbMirror_Checked;
			_cbMirror.Unchecked += _cbMirror_Unchecked;
			_color.ColorChanged += _color_ColorChanged;
			_color.PreviewColorChanged += _color_PreviewColorChanged;
			_color.PreviewUpdateInterval = 100;
		}

		private void _color_PreviewColorChanged(object sender, Color color) {
			if (!_eventsEnabled) return;

			_act[_actionIndex, _frameIndex, _layerIndex].Color = color.ToGrfColor();

			if (_renderer != null) {
				_renderer.Update(_layerIndex);
			}
		}

		private void _color_ColorChanged(object sender, Color value) {
			if (!_eventsEnabled) return;

			_act[_actionIndex, _frameIndex, _layerIndex].Color = _color.InitialColor;
			_act.Commands.SetColor(_actionIndex, _frameIndex, _layerIndex, _color.Color.ToGrfColor());

			if (_renderer != null) {
				_renderer.Update(_layerIndex);
			}
		}

		private void _cbMirror_Unchecked(object sender, RoutedEventArgs e) {
			if (!_eventsEnabled) return;

			_act.Commands.SetMirror(_actionIndex, _frameIndex, _layerIndex, false);

			if (_renderer != null) {
				_renderer.Update(_layerIndex);
			}
		}

		private void _cbMirror_Checked(object sender, RoutedEventArgs e) {
			if (!_eventsEnabled) return;

			_act.Commands.SetMirror(_actionIndex, _frameIndex, _layerIndex, true);

			if (_renderer != null) {
				_renderer.Update(_layerIndex);
			}
		}

		private void _tbSpriteId_MouseUp(object sender, MouseButtonEventArgs e) {
			if (_renderer == null || _renderer.MainDrawingComponent == null) return;

			if (_renderer.MainDrawingComponent != null && e.ChangedButton == MouseButton.Left) {
				var layer = _renderer.MainDrawingComponent.Get(_layerIndex);

				if (layer != null) {
					if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
						_renderer.Editor.SelectionEngine.SelectUpToFromShift(_layerIndex);
					}
					else {
						_actEditor.SelectionEngine.InvertSelection(layer.LayerIndex);
						layer.QuickRender(_renderer);
					}

					Keyboard.Focus(_renderer.Editor.GridPrimary);
				}
			}
		}

		private static bool _getVal(ClickSelectTextBox2 box, out int ival) {
			return Int32.TryParse(box.Text, out ival);
		}

		/// <summary>
		/// Method used by the LayerDraw component; this method
		/// updates the properties without triggering the events.
		/// </summary>
		/// <param name="ignoreVisibility">if set to <c>true</c> [ignore visibility].</param>
		public void InternalUpdate() {
			_update();
		}

		private void _update(bool disableEvents = true) {
			Layer layer = _layer;

			if (layer == null) {
				return;
			}

			if (disableEvents) {
				_eventsEnabled = false;
			}

			SetLayerIndex(_layerIndex);
			SetSpriteNumber(layer.IsIndexed8() ? layer.SpriteIndex : (_act.Sprite.NumberOfIndexed8Images + layer.SpriteIndex));
			SetOffsetX(layer.OffsetX);
			SetOffsetY(layer.OffsetY);
			SetMirror(layer.Mirror);
			_color.Color = layer.Color.ToColor();
			SetScaleX(layer.ScaleX);
			SetScaleY(layer.ScaleY);
			SetRotation(layer.Rotation);

			if (disableEvents) {
				_eventsEnabled = true;
			}
		}

		public void SetSpriteNumber(int value) {
			if (_currentLayerValues.SpriteNumber == value)
				return;

			_currentLayerValues.SpriteNumber = value;
			_tbSpriteNumber.Text = value.ToString(CultureInfo.InvariantCulture);
		}

		public void SetOffsetX(int value) {
			if (_currentLayerValues.OffsetX == value)
				return;

			_currentLayerValues.OffsetX = value;
			_tbOffsetX.Text = value.ToString(CultureInfo.InvariantCulture);
		}

		public void SetOffsetY(int value) {
			if (_currentLayerValues.OffsetY == value)
				return;

			_currentLayerValues.OffsetY = value;
			_tbOffsetY.Text = value.ToString(CultureInfo.InvariantCulture);
		}

		public void SetScaleX(float value) {
			if (_currentLayerValues.ScaleX == value)
				return;

			_currentLayerValues.ScaleX = value;
			_tbScaleX.Text = value.ToString("0.######", CultureInfo.InvariantCulture);
		}

		public void SetScaleY(float value) {
			if (_currentLayerValues.ScaleY == value)
				return;

			_currentLayerValues.ScaleY = value;
			_tbScaleY.Text = value.ToString("0.######", CultureInfo.InvariantCulture);
		}

		public void SetRotation(int value) {
			if (_currentLayerValues.Rotation == value)
				return;

			_currentLayerValues.Rotation = value;
			_tbRotation.Text = value.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Changes the source of the layer control.
		/// </summary>
		/// <param name="act">The act.</param>
		/// <param name="actionIndex">Index of the action.</param>
		/// <param name="frameIndex">Index of the frame.</param>
		/// <param name="layerIndex">Index of the layer.</param>
		/// <param name="partialUpdate">if set to <c>true</c> [partial update].</param>
		public void Set(Act act, int actionIndex, int frameIndex, int layerIndex) {
			_act = act;
			_actionIndex = actionIndex;
			_frameIndex = frameIndex;
			_layerIndex = layerIndex;

			if (_cbMirror.Visibility != Visibility.Visible)
				_cbMirror.Visibility = Visibility.Visible;
			if (_color.Visibility != Visibility.Visible)
				_color.Visibility = Visibility.Visible;

			DrawSelection();
		}

		public void SetFocus(LayerVisualEditor.EditableDataValues col, LayerVisualEditor layerVisualEditor) {
			switch (col) {
				case LayerVisualEditor.EditableDataValues.SpriteNumber:
				case LayerVisualEditor.EditableDataValues.OffsetX:
				case LayerVisualEditor.EditableDataValues.OffsetY:
				case LayerVisualEditor.EditableDataValues.ScaleX:
				case LayerVisualEditor.EditableDataValues.ScaleY:
				case LayerVisualEditor.EditableDataValues.Rotation:
					layerVisualEditor.SetEditBox(LayerIndex, (int)col);
					break;
				case LayerVisualEditor.EditableDataValues.Mirror:
					Keyboard.Focus(_cbMirror);
					break;
				case LayerVisualEditor.EditableDataValues.Color:
					Keyboard.Focus(_color);
					break;
			}
		}

		public void SetLayerValue(string text, LayerVisualEditor.EditableDataValues editValue) {
			int value;
			float valueF;

			switch (editValue) {
				case LayerVisualEditor.EditableDataValues.OffsetX:
					if (Int32.TryParse(text, out value))
						_act.Commands.SetOffsetX(_actionIndex, _frameIndex, _layerIndex, value);
					break;
				case LayerVisualEditor.EditableDataValues.OffsetY:
					if (Int32.TryParse(text, out value))
						_act.Commands.SetOffsetY(_actionIndex, _frameIndex, _layerIndex, value);
					break;
				case LayerVisualEditor.EditableDataValues.Rotation:
					if (Int32.TryParse(text, out value))
						_act.Commands.SetRotation(_actionIndex, _frameIndex, _layerIndex, value);
					break;
				case LayerVisualEditor.EditableDataValues.SpriteNumber:
					if (Int32.TryParse(text, out value))
						_act.Commands.SetAbsoluteSpriteId(_actionIndex, _frameIndex, _layerIndex, value);
					break;
				case LayerVisualEditor.EditableDataValues.ScaleX:
					if (_getDecimalVal(text, out valueF))
						_act.Commands.SetScaleX(_actionIndex, _frameIndex, _layerIndex, valueF);
					break;
				case LayerVisualEditor.EditableDataValues.ScaleY:
					if (_getDecimalVal(text, out valueF))
						_act.Commands.SetScaleY(_actionIndex, _frameIndex, _layerIndex, valueF);
					break;
				case LayerVisualEditor.EditableDataValues.Color:
				case LayerVisualEditor.EditableDataValues.Mirror:
					throw new InvalidOperationException("Cannot set the layer value for this property through VisualLayer; they are handled on their own.");
			}

			_renderer?.Update(_layerIndex);
		}

		internal int GetColumn(TextBlock tbBlock) {
			if (tbBlock == null)
				return -1;

			return Array.IndexOf(_boxes, tbBlock);
		}

		public TextBlock GetBlockFromCol(int col) {
			return _boxes[col];
		}
	}
}
