using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.InteractionComponent;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using Utilities.Tools;

namespace ActEditor.Core.WPF.FrameEditor {
	/// <summary>
	/// Interaction logic for FrameRenderer.xaml
	/// </summary>
	public partial class FrameRenderer : UserControl, IFrameRenderer {
		// Private fields
		protected List<DrawingComponent> _components = new List<DrawingComponent>();
		protected Point _relativeCenter = new Point(0.5, 0.8);
		protected ZoomEngine _zoomEngine = new ZoomEngine();
		protected List<IDrawingModule> _drawingModules = new List<IDrawingModule>();

		// Properties
		/// <summary>
		/// Gets the background grid for events.
		/// </summary>
		public Grid GridBackground {
			get { return _gridBackground; }
		}

		public IFrameRendererEditor Editor { get; set; }

		/// <summary>
		/// Gets or sets the relative center of the frame renderer. (0.5, 0.5) would show the layers at the center of the frame.
		/// </summary>
		/// <value>
		/// The relative center.
		/// </value>
		public Point RelativeCenter {
			get { return _relativeCenter; }
			set { _relativeCenter = value; }
		}

		/// <summary>
		/// Gets the drawing modules. The drawing modules are used to retrieve a list of all the DrawingComponents and draw them in order.
		/// </summary>
		/// <value>
		/// The drawing modules.
		/// </value>
		public List<IDrawingModule> DrawingModules {
			get { return _drawingModules; }
		}

		/// <summary>
		/// Gets the main ActDraw drawing component, useful to ignore the ActDraw references.
		/// </summary>
		/// <value>
		/// The main drawing component.
		/// </value>
		public ActDraw MainDrawingComponent {
			get { return _components.OfType<ActDraw>().FirstOrDefault(p => p.Primary); }
		}

		public IEditorInteraction InteractionEngine { get; set; }

		/// <summary>
		/// Gets or sets the anchor module.
		/// </summary>
		/// <value>
		/// The anchor module.
		/// </value>
		public AnchorDrawModule AnchorModule { get; set; }
		public FrameRendererEdit Edit { get; set; }
		public bool DisableUpdate { get; set; }

		// Events
		public delegate void ZoomChangedDelegate(object sender, double scale);
		public delegate void ViewerMovedDelegate(object sender, Point relativeCenter);
		
		public event MouseButtonEventHandler FrameMouseUp;
		public event ViewerMovedDelegate ViewerMoved;
		public event ZoomChangedDelegate ZoomChanged;

		public virtual void OnViewerMoved(Point relativecenter) {
			ViewerMovedDelegate handler = ViewerMoved;
			if (handler != null) handler(this, relativecenter);
		}

		public virtual void OnZoomChanged(double scale) {
			ZoomChangedDelegate handler = ZoomChanged;
			if (handler != null) handler(this, scale);
		}

		public virtual void OnFrameMouseUp(MouseButtonEventArgs e) {
			MouseButtonEventHandler handler = FrameMouseUp;
			if (handler != null) handler(this, e);
		}

		// Constructor
		public FrameRenderer() {
			InitializeComponent();

			_components.Add(new GridLine(Orientation.Horizontal));
			_components.Add(new GridLine(Orientation.Vertical));

			_primary.Background = new SolidColorBrush(ActEditorConfiguration.ActEditorBackgroundColor);

			SizeChanged += _renderer_SizeChanged;
			MouseWheel += _renderer_MouseWheel;
		}

		public virtual void Init(IFrameRendererEditor editor) {
			Editor = editor;
			Editor.ReferencesChanged += s => UpdateAndSelect();

			Editor.FrameSelector.FrameChanged += (s, e) => Update();
			Editor.FrameSelector.SpecialFrameChanged += (s, e) => Update();
			Editor.FrameSelector.ActionChanged += (s, e) => Update();

			Editor.ActLoaded += delegate {
				if (Editor.Act == null) return;

				Editor.Act.Commands.CommandUndo += (s, e) => Update();
				Editor.Act.Commands.CommandRedo += (s, e) => Update();
			};

			InteractionEngine = new DefaultInteraction(this, editor);
			AnchorModule = new AnchorDrawModule(this, Editor);
			Edit = new FrameRendererEdit(this, Editor);
		}

		public virtual void SizeUpdate() {
			_updateBackground();

			foreach (var dc in _components) {
				dc.QuickRender(this);
			}
		}

		public virtual void Update() {
			//if (DisableUpdate) return;
			_updateBackground();

			while (_components.Count > 2) {
				_components[2].Remove(this);
				_components.RemoveAt(2);
			}

			foreach (var components in _drawingModules.OrderBy(p => p.DrawingPriority)) {
				_components.AddRange(components.GetComponents());
			}

			foreach (var dc in _components) {
				dc.Render(this);
			}
		}

		public void Update(int layerIndex) {
			var comp = _components.OfType<ActDraw>().FirstOrDefault(p => p.Primary);

			if (comp != null) {
				comp.Render(this, layerIndex);
			}
		}

		public void UpdateAndSelect() {
			Update();
			Editor.SelectionEngine.RefreshSelection();
		}

		protected virtual void _updateBackground() {
			try {
				if (ZoomEngine.Scale < 0.45) {
					((VisualBrush)_gridBackground.Background).Viewport = new Rect(_relativeCenter.X, _relativeCenter.Y, 16d / (_gridBackground.ActualWidth), 16d / (_gridBackground.ActualHeight));
				}
				else {
					((VisualBrush)_gridBackground.Background).Viewport = new Rect(_relativeCenter.X, _relativeCenter.Y, 16d / (_gridBackground.ActualWidth / ZoomEngine.Scale), 16d / (_gridBackground.ActualHeight / ZoomEngine.Scale));
				}
			}
			catch {
			}
		}

		private void _renderer_SizeChanged(object sender, SizeChangedEventArgs e) {
			SizeUpdate();
		}

		private void _renderer_MouseWheel(object sender, MouseWheelEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) return;

			ZoomEngine.Zoom(e.Delta);

			Point mousePosition = e.GetPosition(_primary);

			// The relative center must be moved as well!
			double diffX = mousePosition.X / _primary.ActualWidth - _relativeCenter.X;
			double diffY = mousePosition.Y / _primary.ActualHeight - _relativeCenter.Y;

			_relativeCenter.X = mousePosition.X / _primary.ActualWidth - diffX / ZoomEngine.OldScale * ZoomEngine.Scale;
			_relativeCenter.Y = mousePosition.Y / _primary.ActualHeight - diffY / ZoomEngine.OldScale * ZoomEngine.Scale;

			_cbZoom.SelectedIndex = -1;
			_cbZoom.Text = _zoomEngine.ScaleText;
			OnZoomChanged(ZoomEngine.Scale);
			SizeUpdate();
		}

		private void _cbZoom_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbZoom.SelectedIndex < 0) return;

			_zoomEngine.SetZoom(double.Parse(((string)((ComboBoxItem)_cbZoom.SelectedItem).Content).Replace(" %", "")) / 100f);
			_cbZoom.Text = _zoomEngine.ScaleText;
			OnZoomChanged(_zoomEngine.Scale);
			SizeUpdate();
		}

		private void _cbZoom_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				try {
					string text = _cbZoom.Text;

					text = text.Replace(" ", "").Replace("%", "");
					_cbZoom.SelectedIndex = -1;

					double value = double.Parse(text);

					_zoomEngine.SetZoom(value / 100f);
					_cbZoom.Text = _zoomEngine.ScaleText;
					SizeUpdate();
					OnZoomChanged(_zoomEngine.Scale);
					e.Handled = true;
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
			}
		}

		private void _cbZoom_MouseEnter(object sender, MouseEventArgs e) {
			_cbZoom.Opacity = 1;
			_cbZoom.StaysOpenOnEdit = true;
		}

		private void _cbZoom_MouseLeave(object sender, MouseEventArgs e) {
			if (e.LeftButton == MouseButtonState.Released)
				_cbZoom.Opacity = 0.7;
		}

		public Act Act {
			get { return Editor.Act; }
		}

		public int SelectedAction {
			get { return Editor.SelectedAction; }
		}

		public int SelectedFrame {
			get { return Editor.SelectedFrame; }
		}

		public int CenterX {
			get { return (int)(_primary.ActualWidth * _relativeCenter.X); }
		}

		public int CenterY {
			get { return (int)(_primary.ActualHeight * _relativeCenter.Y); }
		}

		public ZoomEngine ZoomEngine {
			get { return _zoomEngine; }
		}

		public Canvas Canva {
			get { return _primary; }
		}

		public virtual List<DrawingComponent> Components {
			get { return _components; }
		}
	}
}
