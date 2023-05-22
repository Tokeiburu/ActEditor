using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using GRF.Core;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;

namespace ActEditor.Tools.GrfShellExplorer.PreviewTabs {
	/// <summary>
	/// Interaction logic for PreviewImage.xaml
	/// Class imported from GrfEditor
	/// </summary>
	public partial class PreviewImage : FilePreviewTab {
		private readonly GrfImageWrapper _wrapper = new GrfImageWrapper();
		private readonly GrfImageWrapper _wrapper2 = new GrfImageWrapper();
		private TransformGroup _regularTransformGroup = new TransformGroup();

		public PreviewImage() {
			InitializeComponent();
			_loadOtherTransformGroup();
			_imagePreview.RenderTransform = _regularTransformGroup;
			_isInvisibleResult = () => _imagePreview.Dispatch(p => p.Visibility = Visibility.Hidden);
		}

		public ScrollViewer ScrollViewer {
			get { return _scrollViewer; }
		}

		private void _loadOtherTransformGroup() {
			_regularTransformGroup = new TransformGroup();
			ScaleTransform flipTrans = new ScaleTransform();
			TranslateTransform translate = new TranslateTransform();
			RotateTransform rotate = new RotateTransform();
			translate.X = 0;
			translate.Y = 0;
			rotate.Angle = 0;
			flipTrans.ScaleX = 1;
			flipTrans.ScaleY = 1;

			_regularTransformGroup.Children.Add(rotate);
			_regularTransformGroup.Children.Add(flipTrans);
			_regularTransformGroup.Children.Add(translate);
		}

		private void _menuItemImageExport_Click(object sender, RoutedEventArgs e) {
			if (_wrapper.Image != null)
				_wrapper.Image.SaveTo(_entry.RelativePath, PathRequest.ExtractSetting);
		}

		protected override void _load(FileEntry entry) {
			try {
				string fileName = entry.RelativePath;

				_imagePreview.Dispatch(p => p.Tag = Path.GetFileNameWithoutExtension(fileName));
				_labelHeader.Dispatch(p => p.Content = "Image preview : " + Path.GetFileName(fileName));

				_wrapper.Image = ImageProvider.GetImage(_grfData.FileTable[fileName].GetDecompressedData(), Path.GetExtension(fileName).ToLower());

				_imagePreview.Dispatch(p => p.Source = _wrapper.Image.Cast<BitmapSource>());
				_imagePreview.Dispatch(p => p.Visibility = Visibility.Visible);
				_scrollViewer.Dispatch(p => p.Visibility = Visibility.Visible);
			}
			catch { }
		}

		private void _buttonExportAt_Click(object sender, RoutedEventArgs e) {
			if (_wrapper.Image != null)
				_wrapper.Image.SaveTo(_entry.RelativePath, PathRequest.ExtractSetting);
		}

		private void _menuItemImageExport2_Click(object sender, RoutedEventArgs e) {
			if (_wrapper2.Image != null)
				_wrapper2.Image.SaveTo(_imagePreviewSprite.Tag.ToString(), PathRequest.ExtractSetting);
		}
	}
}