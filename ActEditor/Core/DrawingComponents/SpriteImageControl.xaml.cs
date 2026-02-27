using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.FrameEditor;
using GRF.GrfSystem;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// Interaction logic for SpriteImageControl.xaml
	/// </summary>
	public partial class SpriteImageControl : UserControl {
		public static readonly DependencyProperty SpriteIndexProperty = DependencyProperty.Register(nameof(SpriteIndex), typeof(int), typeof(SpriteImageControl), new PropertyMetadata(0));
		private IFrameRendererEditor _editor;
		private SpriteSelector _spriteSelector;
		public static SolidColorBrush SpriteBorderBrush = Brushes.Red;
		private string _toolTip;

		public bool Selected { get; set; }
		public bool IsPreviewing { get; set; }
		public int ContentWidth => (int)(_image.Width + 2);

		static SpriteImageControl() {
			SpriteBorderBrush = new SolidColorBrush(ActEditorConfiguration.ActEditorSpriteSelectionBorder.Get().ToColor());
			SpriteBorderBrush.Freeze();
		}

		public int SpriteIndex {
			get => (int)GetValue(SpriteIndexProperty);
			set => SetValue(SpriteIndexProperty, value);
		}

		public SpriteImageControl() {
			InitializeComponent();
		}

		public SpriteImageControl(IFrameRendererEditor editor, SpriteSelector spriteSelector, int spriteId) : this() {
			_editor = editor;
			_spriteSelector = spriteSelector;
			SpriteIndex = spriteId;
			UpdateSize();

			_borderEvents.MouseEnter += _borderEvents_MouseEnter;
			_borderEvents.MouseDown += _borderEvents_MouseDown;
			_borderEvents.MouseMove += _borderEvents_MouseMove;
			_borderEvents.MouseLeave += _borderEvents_MouseLeave;
		}

		private void _borderEvents_MouseLeave(object sender, MouseEventArgs e) {
			if (_spriteSelector._contextMenuImages.IsOpen)
				return;

			ShowSelected(false);
		}

		private void _borderEvents_MouseMove(object sender, MouseEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed) {
				ShowSelected(true);

				DataObject data = new DataObject();

				try {
					GrfImage image = _editor.Act.Sprite.Images[SpriteIndex];
					string file = GrfPath.Combine(Settings.TempPath, String.Format("image_{0:000}", SpriteIndex) + (image.GrfImageType == GrfImageType.Indexed8 ? ".bmp" : ".png"));
					image.Save(file);
					data.SetData(DataFormats.FileDrop, new string[] { file });
				}
				catch {
				}

				data.SetData("ImageIndex", SpriteIndex);
				bool isDragged = true;

				if (_editor.FrameRenderer is PrimaryFrameRenderer) {
					((PrimaryFrameRenderer)_editor.FrameRenderer).AutoRemovePreviewLayer(() => isDragged);
				}

				DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
				isDragged = false;
				ShowSelected(false);

				if (_editor.LayerEditor != null)
					_editor.LayerEditor._lineMoveLayer.Visibility = Visibility.Hidden;

				e.Handled = true;
			}
		}

		private void _borderEvents_MouseDown(object sender, MouseButtonEventArgs e) {
			ShowSelected(true);
		}

		public void ShowSelected(bool value) {
			if (Selected == value)
				return;

			if (value) {
				_borderEvents.BorderBrush = SpriteBorderBrush;
				_spriteSelector.UpdateAllHoverExcept(this);
			}
			else
				_borderEvents.BorderBrush = Brushes.Transparent;

			Selected = value;
		}

		private void _borderEvents_MouseEnter(object sender, MouseEventArgs e) {
			int usage = _editor.Act.FindUsageOf(SpriteIndex).Count;
			GrfImage img = _editor.Act.Sprite.Images[SpriteIndex];

			string toolTip = "Image #" + SpriteIndex + "\r\nWidth = " + img.Width + "\r\nHeight = " + img.Height + "\r\nFormat = " + (img.GrfImageType == GrfImageType.Indexed8 ? "Indexed8" : "Bgra32") + "\r\nUsed in " + usage + " layer(s)";
			
			if (toolTip != _toolTip) {
				_toolTip = toolTip;

				if (_borderEvents.ToolTip == null)
					_borderEvents.ToolTip = new TextBlock();

				TextBlock block = (TextBlock)_borderEvents.ToolTip;
				block.Text = _toolTip;
				_toolTip = toolTip;
			}

			ShowSelected(true);
		}

		public void UpdateSize() {
			var grfImage = _editor.Act.Sprite.Images[SpriteIndex];
			_image.Width = grfImage.Width;
			_image.Height = grfImage.Height;
		}

		private int _previousHash;

		public void UpdateImage() {
			var grfImage = _editor.Act.Sprite.Images[SpriteIndex];

			if (grfImage.GrfImageType == GrfImageType.Indexed8 && grfImage.Palette[3] != 0)
				grfImage.Palette[3] = 0;

			if (grfImage.GetHashCode() == _previousHash)
				return;

			_image.Width = grfImage.Width;
			_image.Height = grfImage.Height;
			_image.Source = grfImage.Cast<BitmapSource>();
			_previousHash = grfImage.GetHashCode();
		}
	}
}
