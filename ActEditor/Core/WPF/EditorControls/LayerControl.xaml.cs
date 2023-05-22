using System;
using System.Collections.Generic;
using System.Globalization;
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
	public partial class LayerControl : UserControl {
		#region Delegates

		public delegate void LayerPropertyChangedDelegate(object sender);

		#endregion

		public static double ActualHeightBuffered;
		private static Size _bufferedSize;

		private readonly List<ClickSelectTextBox2> _boxes = new List<ClickSelectTextBox2>(8);
		private readonly FrameRenderer _renderer;
		private readonly bool _isReference;
		private readonly string _name;
		private readonly StackPanel _sp;
		private readonly ScrollViewer _sv;

		private Act _act;
		private int _actionIndex;

		private bool _eventsEnabled = true;
		private int _frameIndex;
		private bool _hasBuffered;
		private bool _isPreviewSelected;
		private bool _isSelected;
		private int _layerIndex;
		private TabAct _actEditor;
		private bool _dirty;

		public bool Dirty {
			get { return _dirty; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LayerControl" /> class.
		/// This constructure is only meant to be used for references.
		/// </summary>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="header"> </param>
		/// <param name="name"> </param>
		public LayerControl(TabAct actEditor, LayerControlHeader header, string name) {
			_renderer = (FrameRenderer)actEditor.FrameRenderer;
			InitializeComponent();
			_isReference = true;
			_commonInit();
			_initReferenceEvents();
			_name = name;

			header.SizeChanged += delegate {
				for (int i = 2; i < _grid.ColumnDefinitions.Count; i++) {
					_grid.ColumnDefinitions[i].Width = new GridLength(header.Grid.ColumnDefinitions[i].ActualWidth);
					_grid.ColumnDefinitions[i].MinWidth = _grid.ColumnDefinitions[i].Width.Value;
					_grid.ColumnDefinitions[i].MaxWidth = _grid.ColumnDefinitions[i].Width.Value;
				}
			};

			_saveReference();

			Loaded += delegate { ActualHeightBuffered = ActualHeight; };
		}

		public LayerControl(Act act, TabAct actEditor, int layerIndex) {
			_act = act;
			_renderer = (FrameRenderer)actEditor.FrameRenderer;
			_sv = actEditor._layerEditor._sv;
			_sp = actEditor._layerEditor._sp;
			_actEditor = actEditor;

			InitializeComponent();
			_commonInit();
			_layerIndex = layerIndex;

			_tbSpriteId.Text = _layerIndex.ToString(CultureInfo.InvariantCulture);

			_initEvents();
		}

		public Grid Grid {
			get { return _grid; }
		}

		/// <summary>
		/// Attempts to get the current layer (shortchut).
		/// </summary>
		private Layer _layer {
			get { return _act.TryGetLayer(_actionIndex, _frameIndex, _layerIndex); }
		}

		/// <summary>
		/// Gets or sets a value indicating whether this control is selected.
		/// </summary>
		public bool IsSelected {
			get { return _isSelected; }
			set {
				if (_isSelected == value) return;

				_isSelected = value;

				_grid.Background = _isSelected ? (Brush)this.FindResource("UIThemeLayerControlSelectedBrush") : (Brush)this.FindResource("UIThemeTextBoxBackgroundBrush");
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

				if (IsSelected) return;

				_grid.Background = _isPreviewSelected ? (Brush)this.FindResource("UIThemeLayerControlPreviewSelectedBrush") : (Brush)this.FindResource("UIThemeTextBoxBackgroundBrush");
			}
		}

		private void _saveReference() {
			ActEditorConfiguration.ConfigAsker.IsAutomaticSaveEnabled = false;
			List<string> data = Methods.StringToList(ActEditorConfiguration.ConfigAsker["[ActEditor - " + _name + "]", "0,0,0,false,#FFFFFFFF,1,1,0"]);

			_tbOffsetX.Text = data[1];
			_tbOffsetY.Text = data[2];
			_cbMirror.IsChecked = Boolean.Parse(data[3]);
			_color.Color = new GrfColor(data[4]).ToColor();
			_tbScaleX.Text = data[5];
			_tbScaleY.Text = data[6];
			_tbRotation.Text = data[7];

			ActEditorConfiguration.ConfigAsker.IsAutomaticSaveEnabled = true;
		}

		public event LayerPropertyChangedDelegate LayerPropertyChanged;

		public void OnLayerPropertyChanged() {
			LayerPropertyChangedDelegate handler = LayerPropertyChanged;
			if (handler != null) handler(this);
		}

		private void _initReferenceEvents() {
			TextChangedEventHandler tceh = delegate(object sender, TextChangedEventArgs args) {
				if (!_eventsEnabled) return;

				int ival;
				if (_getVal(sender as ClickSelectTextBox2, out ival)) {
					_applyCommands();
				}
			};

			TextChangedEventHandler tcehf = delegate(object sender, TextChangedEventArgs args) {
				if (!_eventsEnabled) return;

				float dval;
				if (_getDecimalVal(((ClickSelectTextBox2)sender).Text, out dval)) {
					_applyCommands();
				}
			};

			RoutedEventHandler update = delegate {
				if (!_eventsEnabled) return;

				_applyCommands();
			};

			_tbOffsetX.TextChanged += tceh;
			_tbOffsetY.TextChanged += tceh;
			_cbMirror.Checked += update;
			_cbMirror.Unchecked += update;
			_color.ColorChanged += (e, a) => update(null, null);
			_color.PreviewColorChanged += (e, a) => update(null, null);
			_tbScaleX.TextChanged += tcehf;
			_tbScaleY.TextChanged += tcehf;
			_tbRotation.TextChanged += tceh;
		}

		private void _applyCommands(bool save = true) {
			try {
				if (_act == null) return;

				_act.Commands.UndoAll();

				_act.Commands.Translate(Int32.Parse(_tbOffsetX.Text), Int32.Parse(_tbOffsetY.Text));
				_act.Commands.Scale(_getDecimalVal(_tbScaleX.Text), _getDecimalVal(_tbScaleY.Text));
				_act.Commands.Rotate(Int32.Parse(_tbRotation.Text));

				if (_cbMirror.IsChecked == true)
					_act.Commands.SetMirror(_cbMirror.IsChecked == true);

				if (!_color.Color.ToGrfColor().Equals(GrfColor.White))
					_act.Commands.SetColor(_color.Color.ToGrfColor());

				if (save) {
					List<string> data = new List<string> {
						"",
						_tbOffsetX.Text,
						_tbOffsetY.Text,
						_cbMirror.IsChecked.ToString(),
						_color.Color.ToGrfColor().ToHexString(),
						_tbScaleX.Text,
						_tbScaleY.Text,
						_tbRotation.Text
					};

					ActEditorConfiguration.ConfigAsker["[ActEditor - " + _name + "]"] = Methods.ListToString(data);
				}
			}
			catch {
			}

			if (_renderer != null) {
				_renderer.Update();
			}
		}

		private static float _getDecimalVal(string text) {
			float dval;
			_getDecimalVal(text, out dval);
			return dval;
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

			if (!_isReference) {
				_grid.Loaded += delegate {
					for (int i = 0; i < _grid.ColumnDefinitions.Count; i++) {
						_grid.ColumnDefinitions[i].Width = new GridLength(_actEditor._layerEditor._sfch.Grid.ColumnDefinitions[i].ActualWidth);
						_grid.ColumnDefinitions[i].MinWidth = _grid.ColumnDefinitions[i].Width.Value;
						_grid.ColumnDefinitions[i].MaxWidth = _grid.ColumnDefinitions[i].Width.Value;
					}
				};
			}
		}

		/// <summary>
		/// Initializes the events
		/// </summary>
		private void _initEvents() {
			_boxes.Add(_tbSpriteNumber);
			_boxes.Add(_tbOffsetX);
			_boxes.Add(_tbOffsetY);
			_boxes.Add(_tbScaleX);
			_boxes.Add(_tbScaleY);
			_boxes.Add(_tbRotation);

			_tbSpriteId.MouseUp += _tbSpriteId_MouseUp;
			_tbSpriteNumber.TextChanged += _tbSpriteNumber_TextChanged;
			_tbOffsetX.TextChanged += _tbOffsetX_TextChanged;
			_tbOffsetY.TextChanged += _tbOffsetY_TextChanged;
			_cbMirror.Checked += _cbMirror_Checked;
			_cbMirror.Unchecked += _cbMirror_Unchecked;
			_color.ColorChanged += _color_ColorChanged;
			_color.PreviewColorChanged += _color_PreviewColorChanged;
			_color.PreviewUpdateInterval = 100;
			_tbScaleX.TextChanged += _tbScaleX_TextChanged;
			_tbScaleY.TextChanged += _tbScaleY_TextChanged;
			_tbRotation.TextChanged += _tbRotation_TextChanged;
		}

		private void _tbRotation_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			int ival;
			if (_getVal(sender as ClickSelectTextBox2, out ival)) {
				_act.Commands.SetRotation(_actionIndex, _frameIndex, _layerIndex, ival);

				if (_renderer != null) {
					_renderer.Update(_layerIndex);
				}
			}
		}

		private void _tbScaleX_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			float dval;
			if (_getDecimalVal(((TextBox) sender).Text, out dval)) {
				_act.Commands.SetScaleX(_actionIndex, _frameIndex, _layerIndex, dval);

				if (_renderer != null) {
					_renderer.Update(_layerIndex);
				}
			}
		}

		private void _tbScaleY_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			float dval;
			if (_getDecimalVal(((TextBox) sender).Text, out dval)) {
				_act.Commands.SetScaleY(_actionIndex, _frameIndex, _layerIndex, dval);

				if (_renderer != null) {
					_renderer.Update(_layerIndex);
				}
			}
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

		private void _tbOffsetY_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			int ival;
			if (_getVal(sender as ClickSelectTextBox2, out ival)) {
				_act.Commands.SetOffsetY(_actionIndex, _frameIndex, _layerIndex, ival);

				if (_renderer != null) {
					_renderer.Update(_layerIndex);
				}
			}
		}

		private void _tbOffsetX_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			int ival;
			if (_getVal(sender as ClickSelectTextBox2, out ival)) {
				_act.Commands.SetOffsetX(_actionIndex, _frameIndex, _layerIndex, ival);

				if (_renderer != null) {
					_renderer.Update(_layerIndex);
				}
			}
		}

		private void _tbSpriteNumber_TextChanged(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			int ival;
			if (_getVal(sender as ClickSelectTextBox2, out ival)) {
				_act.Commands.SetAbsoluteSpriteId(_actionIndex, _frameIndex, _layerIndex, ival);

				if (_renderer != null) {
					_renderer.Update(_layerIndex);
				}
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
						layer.IsSelected = !layer.IsSelected;
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
		/// Fully updates the properties and trigger events.
		/// </summary>
		public void Update() {
			_update();
			OnLayerPropertyChanged();
		}

		/// <summary>
		/// Method used by the LayerDraw component; this method
		/// updates the properties without triggering the events.
		/// </summary>
		/// <param name="ignoreVisibility">if set to <c>true</c> [ignore visibility].</param>
		public void InternalUpdate(bool ignoreVisibility = false) {
			if (ignoreVisibility || _isVisible()) {
				_update();
			}
			else {
				_dirty = true;
			}
		}

		private void _update(bool disableEvents = true) {
			Layer layer = _layer;
			_dirty = false;

			if (layer == null) {
				SetNull();
				return;
			}

			if (disableEvents) {
				_eventsEnabled = false;
			}

			_tbSpriteId.Text = _layerIndex.ToString(CultureInfo.InvariantCulture);

			if (layer.IsIndexed8()) {
				_tbSpriteNumber.Text = layer.SpriteIndex.ToString(CultureInfo.InvariantCulture);
			}
			else {
				_tbSpriteNumber.Text = (_act.Sprite.NumberOfIndexed8Images + layer.SpriteIndex).ToString(CultureInfo.InvariantCulture);
			}

			_tbOffsetX.Text = layer.OffsetX.ToString(CultureInfo.InvariantCulture);
			_tbOffsetY.Text = layer.OffsetY.ToString(CultureInfo.InvariantCulture);
			_cbMirror.IsChecked = layer.Mirror;
			_color.Color = layer.Color.ToColor();
			_tbScaleX.Text = layer.ScaleX.ToString(CultureInfo.InvariantCulture);
			_tbScaleY.Text = layer.ScaleY.ToString(CultureInfo.InvariantCulture);
			_tbRotation.Text = layer.Rotation.ToString(CultureInfo.InvariantCulture);

			if (disableEvents) {
				_eventsEnabled = true;
			}
		}

		private void __update() {
			ClickSelectTextBox.EventsEnabled = false;
			_update(true);
			ClickSelectTextBox.EventsEnabled = true;
		}

		private void _updateReference(bool disableEvents = true) {
			if (disableEvents)
				_eventsEnabled = false;

			_tbSpriteNumber.Text = "";
			_tbSpriteNumber.IsHitTestVisible = false;
			_tbSpriteNumber.IsReadOnly = true;

			_saveReference();

			if (disableEvents)
				_eventsEnabled = true;
		}

		/// <summary>
		/// Changes the source of the layer control.
		/// </summary>
		/// <param name="act">The act.</param>
		/// <param name="actionIndex">Index of the action.</param>
		/// <param name="frameIndex">Index of the frame.</param>
		/// <param name="layerIndex">Index of the layer.</param>
		/// <param name="partialUpdate">if set to <c>true</c> [partial update].</param>
		public void Set(Act act, int actionIndex, int frameIndex, int layerIndex, bool partialUpdate) {
			_act = act;
			_actionIndex = actionIndex;
			_frameIndex = frameIndex;
			_layerIndex = layerIndex;

			_cbMirror.Visibility = Visibility.Visible;
			_color.Visibility = Visibility.Visible;

			if (_isReference) {
				_updateReference();
			}
			else {
				if (partialUpdate)
					__update();
				else {
					_actEditor.LayerEditor.AsyncUpdateLayerControl(layerIndex);
					//_update();
				}
			}
		}

		private bool _isVisible() {
			if (_sp == null || _sv == null) return true;

			int index = _sp.Children.IndexOf(this);

			if (_layerIndex < 0 || _layerIndex >= _sp.Children.Count) return false;

			double offsetYStart = index * ActualHeight;
			double offsetYEnd = (index + 1) * ActualHeight;

			if (_sv.VerticalOffset < offsetYStart && offsetYStart < _sv.VerticalOffset + _sv.ViewportHeight ||
			    _sv.VerticalOffset < offsetYEnd && offsetYEnd < _sv.VerticalOffset + _sv.ViewportHeight)
				return true;

			return false;
		}

		protected override void OnRender(DrawingContext drawingContext) {
			if (_isVisible()) {
				base.OnRender(drawingContext);
			}
		}

		protected override Size MeasureOverride(Size constraint) {
			if (_hasBuffered) {
				return _bufferedSize;
			}

			if (!_isReference) {
				_bufferedSize = base.MeasureOverride(constraint);
				_hasBuffered = true;
				return _bufferedSize;
			}

			return base.MeasureOverride(constraint);
		}

		public void ReferenceSetAndUpdate(Act act, int actionIndex, int frameIndex, int layerIndex, bool partialUpdate) {
			if (!_isReference) return;

			_act = act;
			_actionIndex = actionIndex;
			_frameIndex = frameIndex;
			_layerIndex = layerIndex;

			_cbMirror.Visibility = Visibility.Visible;
			_color.Visibility = Visibility.Visible;

			_updateReference();
			_applyCommands(false);
		}

		/// <summary>
		/// Sets the control to 'nothing' without removing itself.
		/// </summary>
		public void SetNull() {
			if (_isReference) {
				_updateReference();
			}
			else {
				_eventsEnabled = false;
				ClickSelectTextBox.EventsEnabled = false;

				_tbSpriteId.Text = "";
				_boxes.ForEach(p => p.Text = "");
				_cbMirror.Visibility = Visibility.Hidden;
				_color.Visibility = Visibility.Hidden;

				_eventsEnabled = true;
				ClickSelectTextBox.EventsEnabled = true;
			}
		}

		/// <summary>
		/// Refreshes the background manually (refresh is disabled when animations are playing).
		/// </summary>
		public void RefreshPreviewBackground() {
			_boxes.ForEach(p => p.UpdateBackground());
		}
	}
}