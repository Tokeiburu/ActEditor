using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.DrawingComponents;
using ErrorManager;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Core.WPF.FrameEditor {
	public class AnchorDrawModule : IDrawingModule {
		private readonly IFrameRendererEditor _editor;
		private readonly FrameRenderer _renderer;
		private bool _anchorEdit;
		private Point _oldAnchorPoint;

		public int AnchorIndex { get; set; }
		public int DrawingPriority => (int)DrawingPriorityValues.Highest;
		public bool Permanent => false;

		public List<DrawingComponent> GetComponents() {
			if (_editor.Act != null) {
				Frame frame = _editor.Act.TryGetFrame(_editor.SelectedAction, _editor.SelectedFrame);

				if (frame != null && ActEditorConfiguration.ShowAnchors && frame.Anchors.Count > 0) {
					return frame.Anchors.Select(p => (DrawingComponent)new AnchorDraw(p) { Visible = true }).ToList();
				}
			}

			return new List<DrawingComponent>();
		}

		public AnchorDrawModule(FrameRenderer renderer, IFrameRendererEditor editor) {
			_editor = editor;
			_renderer = renderer;
			_renderer.GridBackground.LostMouseCapture += _gridBackground_LostMouseCapture;
		}

		public void EditAnchors() {
			if (_editor.IndexSelector.IsPlaying) return;
			if (_renderer.GridBackground.IsMouseCaptured) return;
			if (_anchorEdit) return;
			if (!ActEditorConfiguration.ShowAnchors) {
				ErrorHandler.HandleException("You must turn on the anchors by going in Anchors > Show anchors before editing them.");
				return;
			}

			_anchorEdit = true;

			if (AnchorIndex >= _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].Anchors.Count) {
				_editor.Act.Commands.SetAnchorPosition(_editor.SelectedAction, _editor.SelectedFrame, 0, 0, AnchorIndex);
				_renderer.Update();
			}

			Anchor anchor = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].Anchors[AnchorIndex];

			_oldAnchorPoint = new Point(anchor.OffsetX, anchor.OffsetY);

			_renderer.GridBackground.CaptureMouse();
			_editor.Element.PreviewKeyDown += _actEditor_PreviewKeyDown;
			_renderer.GridBackground.PreviewMouseMove += _gridBackground_MouseMove;
			_renderer.GridBackground.PreviewMouseUp += _gridBackground_MouseUp;
			_renderer.GridBackground.PreviewMouseDown += _gridBackground_MouseDown;

			_setAnchorPosition(Mouse.GetPosition(_renderer.GridBackground));
		}

		private void _setAnchorPosition(Point absolutePosition) {
			if (!_anchorEdit) return;

			int offsetX = (int)((absolutePosition.X - _editor.FrameRenderer.CenterX) / _editor.FrameRenderer.ZoomEngine.Scale);
			int offsetY = (int)((absolutePosition.Y - _editor.FrameRenderer.CenterY) / _editor.FrameRenderer.ZoomEngine.Scale);

			Point point = new Point(offsetX, offsetY);
			Anchor anchor = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].Anchors[AnchorIndex];
			anchor.OffsetX = offsetX;
			anchor.OffsetY = offsetY;

			var anchors = _editor.FrameRenderer.Components.OfType<AnchorDraw>().ToList();

			if (AnchorIndex < anchors.Count) {
				var anchorDraw = anchors[AnchorIndex];

				if (anchorDraw != null) {
					anchorDraw.RenderOffsets(_editor.FrameRenderer, point);
				}
			}

			var references = _editor.FrameRenderer.Components.OfType<ActDraw>().Where(p => !p.Primary);

			foreach (var dc in references) {
				dc.Render(_editor.FrameRenderer);
			}
		}

		private void _gridBackground_MouseMove(object sender, MouseEventArgs e) {
			if (!_anchorEdit) return;
			_setAnchorPosition(e.GetPosition(_renderer.GridBackground));
			e.Handled = true;
		}

		private void _gridBackground_LostMouseCapture(object sender, MouseEventArgs e) {
			if (!_anchorEdit) return;

			try {
				_editor.Element.PreviewKeyDown -= _actEditor_PreviewKeyDown;
				_renderer.GridBackground.PreviewMouseMove -= _gridBackground_MouseMove;
				_renderer.GridBackground.PreviewMouseUp -= _gridBackground_MouseUp;
				_renderer.GridBackground.PreviewMouseDown -= _gridBackground_MouseDown;

				Anchor anchor = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].Anchors[AnchorIndex];

				int offsetX = anchor.OffsetX;
				int offsetY = anchor.OffsetY;

				anchor.OffsetX = (int)_oldAnchorPoint.X;
				anchor.OffsetY = (int)_oldAnchorPoint.Y;

				_editor.Act.Commands.SetAnchorPosition(_editor.SelectedAction, _editor.SelectedFrame, offsetX, offsetY, AnchorIndex);
			}
			catch {
			}
			finally {
				_anchorEdit = false;
			}
		}

		private void _gridBackground_MouseUp(object sender, MouseButtonEventArgs e) {
			_setAnchorPosition(e.GetPosition(_renderer.GridBackground));
			_renderer.GridBackground.ReleaseMouseCapture();
			e.Handled = true;
		}

		private void _gridBackground_MouseDown(object sender, MouseButtonEventArgs e) {
			e.Handled = true;
		}

		private void _actEditor_PreviewKeyDown(object sender, KeyEventArgs e) {
			_renderer.GridBackground.ReleaseMouseCapture();
		}
	}
}
