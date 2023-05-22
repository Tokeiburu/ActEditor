using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Tools.GrfShellExplorer;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.IO;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;
using Utilities.Extension;
using Action = System.Action;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for ReferenceControl.xaml
	/// </summary>
	public partial class ReferenceControl : UserControl {
		#region Delegates

		public delegate void ReferenceFrameEventHandler(object sender);

		#endregion

		private readonly TabAct _actEditor;
		private readonly string _defaultFemale;
		private readonly string _defaultMale;
		private readonly List<FancyButton> _fancyButtons;
		private readonly LayerControl _layerControl;
		private readonly string _name;
		private string _filePath;
		private ZMode _mode;
		private bool _sex;
		private bool _directional;

		public ReferenceControl() {
			InitializeComponent();
		}

		public ReferenceControl(TabAct actEditor, string defaultMale, string defaultFemale, string name, bool directional) {
			InitializeComponent();

			try {
				if (directional) {
					_fancyButtons = new FancyButton[] {_fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7}.ToList();
					BitmapSource image = ApplicationManager.PreloadResourceImage("arrow.png");
					BitmapSource image2 = ApplicationManager.PreloadResourceImage("arrowoblique.png");

					_fancyButtons.ForEach(p => p.ImageIcon.Stretch = Stretch.Uniform);

					_fancyButton0.ImageIcon.Source = image;
					_fancyButton0.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton0.ImageIcon.RenderTransform = new RotateTransform {Angle = 90};

					_fancyButton1.ImageIcon.Source = image2;
					_fancyButton1.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton1.ImageIcon.RenderTransform = new RotateTransform {Angle = 90};

					_fancyButton2.ImageIcon.Source = image;
					_fancyButton2.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton2.ImageIcon.RenderTransform = new RotateTransform {Angle = 180};

					_fancyButton3.ImageIcon.Source = image2;
					_fancyButton3.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton3.ImageIcon.RenderTransform = new RotateTransform {Angle = 180};

					_fancyButton4.ImageIcon.Source = image;
					_fancyButton4.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton4.ImageIcon.RenderTransform = new RotateTransform {Angle = 270};

					_fancyButton5.ImageIcon.Source = image2;
					_fancyButton5.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton5.ImageIcon.RenderTransform = new RotateTransform {Angle = 270};

					_fancyButton6.ImageIcon.Source = image;
					_fancyButton6.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton6.ImageIcon.RenderTransform = new RotateTransform {Angle = 360};

					_fancyButton7.ImageIcon.Source = image2;
					_fancyButton7.ImageIcon.RenderTransformOrigin = new Point(0.5, 0.5);
					_fancyButton7.ImageIcon.RenderTransform = new RotateTransform {Angle = 360};

					_grid.Visibility = Visibility.Visible;
				}
			}
			catch {
			}

			_directional = directional;
			_layerControl = new LayerControl(actEditor, _header, name);
			_layerControl.IsEnabled = false;
			_sp.Children.Add(_layerControl);

			TextBlock tb = (TextBlock) _refZState.FindName("_tbIdentifier");
			tb.Margin = new Thickness(2);
			tb.FontSize = 12;
			tb.Foreground = Brushes.Black;

			Grid grid = ((Grid) ((Grid) ((Border) _refZState.FindName("_border")).Child).Children[2]);

			grid.HorizontalAlignment = HorizontalAlignment.Stretch;
			grid.Margin = new Thickness(0, 0, 2, 0);
			grid.ColumnDefinitions[0] = new ColumnDefinition();
			grid.ColumnDefinitions[1] = new ColumnDefinition {Width = new GridLength(-1, GridUnitType.Auto)};
			var child1 = grid.Children[0];
			var child2 = grid.Children[1];

			child1.SetValue(Grid.ColumnProperty, 1);
			child2.SetValue(Grid.ColumnProperty, 0);

			_header.HideIdAndSprite();

			_actEditor = actEditor;
			_defaultMale = defaultMale;
			_defaultFemale = defaultFemale;
			_name = name;

			_layerControl._tbSpriteId.Visibility = Visibility.Collapsed;
			_layerControl._tbSpriteNumber.Visibility = Visibility.Collapsed;

			_layerControl.Grid.ColumnDefinitions[0].MinWidth = 0;
			_layerControl.Grid.ColumnDefinitions[0].Width = new GridLength(0);

			_layerControl.Grid.ColumnDefinitions[1].MinWidth = 0;
			_layerControl.Grid.ColumnDefinitions[1].Width = new GridLength(0);

			_layerControl.Grid.ColumnDefinitions[2].MinWidth = 0;
			_layerControl.Grid.ColumnDefinitions[2].Width = new GridLength(0);

			_cbRef.Checked += delegate {
				_layerControl.IsEnabled = true;
				_rectangleVisibility.Visibility = Visibility.Collapsed;
				_rectangleVisibility.IsHitTestVisible = false;

				ActEditorConfiguration.ConfigAsker["[ActEditor - IsEnabled - " + _name + "]"] = true.ToString();
				Update(true);
			};

			_cbRef.Unchecked += delegate {
				_layerControl.IsEnabled = false;
				_rectangleVisibility.Visibility = Visibility.Visible;
				_rectangleVisibility.IsHitTestVisible = true;

				ActEditorConfiguration.ConfigAsker["[ActEditor - IsEnabled - " + _name + "]"] = false.ToString();
				OnUpdated();
				_actEditor.OnReferencesChanged();
			};

			_filePath = ActEditorConfiguration.ConfigAsker["[ActEditor - Path - " + name + "]", ""];

			_actEditor.Loaded += delegate { _cbRef.IsChecked = Boolean.Parse(ActEditorConfiguration.ConfigAsker["[ActEditor - IsEnabled - " + _name + "]", false.ToString()]); };

			_mode = Int32.Parse(ActEditorConfiguration.ConfigAsker["[ActEditor - Mode - " + name + "]", "0"]) == 0 ? ZMode.Front : ZMode.Back;
			_sex = Boolean.Parse(ActEditorConfiguration.ConfigAsker["[ActEditor - Gender - " + name + "]", "true"]);
			_updateGenderButton();

			FilePathChanged += new ReferenceFrameEventHandler(_referenceFrame_FilePathChanged);

			Action action = new Action(delegate {
				bool isFront = Mode == 0;

				_refZState.ImagePath = isFront ? "front.png" : "back.png";
				_refZState.TextHeader = isFront ? "Front" : "Back";
			});

			_refZState.Click += delegate {
				Mode = Mode == ZMode.Front ? ZMode.Back : ZMode.Front;
				action();
			};

			action();

			_referenceFrame_FilePathChanged(null);

			if (name == "Head" || name == "Other" || name == "Body") {
				_buttonAnchor.Visibility = Visibility.Visible;
				_buttonAnchor.IsEnabled = true;

				_cbAnchor.Visibility = Visibility.Visible;
				_cbAnchor.IsEnabled = true;
			}

			_cbRef.Content = name;
			WpfUtils.AddMouseInOutEffectsBox(_cbRef);

			_cbAnchor.DropDownOpened += delegate { _buttonAnchor.IsPressed = true; };

			_cbAnchor.DropDownClosed += delegate {
				_buttonAnchor.IsPressed = false;
				Keyboard.Focus(actEditor._gridPrimary);
			};

			_cbAnchor.SelectionChanged += new SelectionChangedEventHandler(_cbAnchor_SelectionChanged);
			_cbAnchor_SelectionChanged(null, null);

			if (name == "Neighbor") {
				_buttonAnchor.Visibility = Visibility.Collapsed;
				_cbAnchor.Visibility = Visibility.Collapsed;
			}

			_buttonSprite.DragEnter += (s, e) => {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".act"))) {
						_buttonSprite.Background = new SolidColorBrush(Color.FromArgb(255, 138, 247, 160));
						e.Effects = DragDropEffects.All;
					}
				}
			};

			_buttonSprite.DragLeave += (s, e) => {
				_buttonSprite.Background = Brushes.Transparent;
			};

			_buttonSprite.Drop += (s, e) => {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".act"))) {
						FilePath = files.First(p => p.IsExtension(".act"));
						_buttonSprite.Background = Brushes.Transparent;
						e.Handled = true;
					}
				}
			};
		}

		private void _updateGenderButton() {
			if (_directional) {
				_gender.Visibility = Visibility.Collapsed;
				return;
			}

			if (!String.IsNullOrEmpty(_filePath)) {
				_gender.Visibility = Visibility.Collapsed;
			}
			else {	
				_gender.IsPressed = _sex;
				_gender.ImagePath = _sex ? "female.png" : "male.png";
			}
		}

		public bool ShowReference {
			get { return _cbRef.Dispatch(() => _cbRef.IsChecked == true); }
		}

		public ZMode Mode {
			get { return _mode; }
			set {
				if (_mode == value) return;

				_mode = value;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Mode - " + _name + "]"] = value == ZMode.Front ? "0" : "1";

				if (_mode == ZMode.Back) {
					_actEditor.References.Remove(this);
					_actEditor.References.Insert(0, this);
				}
				else {
					_actEditor.References.Remove(this);
					_actEditor.References.Add(this);
				}

				Update(false);
			}
		}

		public Act Act { get; set; }
		public Spr Spr { get; set; }

		public string FilePath {
			get { return _filePath; }
			set {
				_filePath = value;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Path - " + _name + "]"] = value;
				OnFilePathChanged();
				Update(true);
			}
		}

		public event ReferenceFrameEventHandler Updated;

		public void OnUpdated() {
			ReferenceFrameEventHandler handler = Updated;
			if (handler != null) handler(this);
		}

		public event ReferenceFrameEventHandler FilePathChanged;

		public void OnFilePathChanged() {
			ReferenceFrameEventHandler handler = FilePathChanged;
			if (handler != null) handler(this);
		}

		public void Init() {
			if (_name == "Head" || _name == "Other" || _name == "Body") {
				if (_name == "Body")
					_cbAnchor.SelectedIndex = 3;

				if (_name == "Head") {
					int previousIndex = 3;

					_actEditor.References.First(p => p._name == "Body")._cbRef.Checked += delegate {
						previousIndex = _cbAnchor.SelectedIndex;
						_cbAnchor.SelectedIndex = 0;
					};
					_actEditor.References.First(p => p._name == "Body")._cbRef.Unchecked += delegate { _cbAnchor.SelectedIndex = previousIndex; };
				}

				_cbAnchor.SelectionChanged += (e, a) => {
					_frame_Updated(e);
					_actEditor.OnReferencesChanged();
				};

				Updated += _frame_Updated;
				//_actEditor.References.First(p => p.Frame._name == "Body").Frame.Updated += new ReferenceFrameEventHandler(_frame_Updated);
				_actEditor.References.First(p => p._name == "Neighbor").Updated += new ReferenceFrameEventHandler(_frame_Updated);
				_actEditor.ActLoaded += e => {
					_frame_Updated(null);

					if (_actEditor.Act != null) {
						foreach (var reference in _actEditor.References) {
							if (reference.Act != null && reference.Act.Name == "Body") {
								if (ActEditorConfiguration.ReverseAnchor) {
									_actEditor.Act.AnchoredTo = reference.Act;
									reference.Act.AnchoredTo = null;
									break;
								}

								reference.RefreshSelection();
								break;
							}
						}
					}

					_actEditor._rendererPrimary.Update();
				};
			}
		}

		public void RefreshSelection() {
			_frame_Updated(null);
		}

		private void _frame_Updated(object sender) {
			ReferenceControl refCtr;
			int index = _cbAnchor.SelectedIndex;

			if (Act != null)
				Act.AnchoredTo = null;

			switch (_cbAnchor.SelectedIndex) {
				case 0:
				case 1:
				case 2:
					refCtr = _actEditor.References.First(p => p._name == (index == 0 ? "Body" : index == 1 ? "Other" : "Neighbor"));
					if (Act != null && ShowReference && refCtr.ShowReference) {
						Act.AnchoredTo = refCtr.Act;
					}
					break;
				case 3:
					if (Act != null && ShowReference && _actEditor.Act != null) {
						Act.AnchoredTo = _actEditor.Act;
					}
					break;
			}

			// There is always a render pass after a frame update or act loading event
		}

		public void Update(bool updateSprite) {
			if (updateSprite) {
				MakeAct(true);
				Act.Name = _name;
			}

			_layerControl.ReferenceSetAndUpdate(Act, _actEditor._frameSelector.SelectedAction, _actEditor._frameSelector.SelectedFrame, 0, false);
			OnUpdated();
			_actEditor.OnReferencesChanged();
		}

		public void Reset() {
			if (_cbAnchor.SelectedIndex == 3) {
				if (Act != null)
					Act.AnchoredTo = null;
			}
		}

		private void _cbAnchor_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			_cbAnchor.Items.Cast<ComboBoxItem>().ToList().ForEach(p => p.SetValue(FontWeightProperty, FontWeights.Normal));

			if (_cbAnchor.SelectedItem != null) {
				((ComboBoxItem) _cbAnchor.SelectedItem).SetValue(FontWeightProperty, FontWeights.Bold);
			}
		}

		private void _referenceFrame_FilePathChanged(object sender) {
			_reset.Visibility = String.IsNullOrEmpty(FilePath) ? Visibility.Hidden : Visibility.Visible;

			if (!_directional)
				_gender.Visibility = String.IsNullOrEmpty(FilePath) ? Visibility.Visible : Visibility.Hidden;
		}

		private void _buttonSprite_Click(object sender) {
			try {
				string fileName = ActEditorConfiguration.ExtractingServiceLastPath;

				if (FilePath != null && File.Exists(FilePath)) {
					fileName = FilePath;
				}

				string file = PathRequest.OpenFileExtract("fileName", fileName, "filter", "Act or Container Files|*.act;*.grf;*.rgz;*.gpf;*.thor|Act Files|*.act|Container Files|*.grf;*.rgz;*.gpf;*.thor");

				if (file != null) {
					if (file.IsExtension(".grf", ".rgz", ".gpf", ".thor")) {
						GrfExplorer explorer = new GrfExplorer(file, SelectMode.Act);
						if (explorer.ShowDialog() == true) {
							file = file + "?" + explorer.SelectedItem;
						}
						else
							return;
					}

					FilePath = file;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _reset_Click(object sender, RoutedEventArgs e) {
			try {
				FilePath = null;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _fancyButton_Click(object sender, RoutedEventArgs e) {
			_fancyButtons.ForEach(p => p.IsPressed = false);

			var fb = ((FancyButton) sender);
			fb.IsPressed = true;

			int offsetX = 0;
			int offsetY = 0;

			switch ((string) fb.Tag) {
				case "0":
					offsetY += 30;
					break;
				case "1":
					offsetX -= 25;
					offsetY += 30;
					break;
				case "2":
					offsetX -= 25;
					break;
				case "3":
					offsetX -= 25;
					offsetY -= 30;
					break;
				case "4":
					offsetY -= 30;
					break;
				case "5":
					offsetX += 25;
					offsetY -= 30;
					break;
				case "6":
					offsetX += 25;
					break;
				case "7":
					offsetX += 25;
					offsetY += 30;
					break;
			}

			_layerControl._tbOffsetX.Text = offsetX.ToString(CultureInfo.InvariantCulture);
			_layerControl._tbOffsetY.Text = offsetY.ToString(CultureInfo.InvariantCulture);
		}

		private void _buttonAnchor_Click(object sender, RoutedEventArgs e) {
			_cbAnchor.IsDropDownOpen = true;
		}

		private void _gender_Click(object sender, RoutedEventArgs e) {
			_sex = !_sex;
			Sex = _sex;
		}

		public string ReferenceName {
			get { return _name; }
		}

		public bool Sex {
			get { return _sex; }
			set {
				_sex = value;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Gender - " + _name + "]"] = value.ToString();
				_updateGenderButton();
				Update(true);
			}
		}

		public void MakeAct(bool force = false) {
			if (Act != null && Spr != null && !force) {
				return;
			}

			byte[] dataAct = ApplicationManager.GetResource((_sex ? _defaultFemale : _defaultMale) + ".act");
			byte[] dataSpr = ApplicationManager.GetResource((_sex ? _defaultFemale : _defaultMale) + ".spr");

			if (FilePath != null && FilePath.IsExtension(".spr", ".act")) {
				TkPath path = FilePath;

				if (File.Exists(path.FilePath)) {
					try {
						dataAct = GrfPath.GetData(path);
						if (dataAct == null)
							throw new FileNotFoundException("File not found : " + path);
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}

					try {
						path = path.GetFullPath().ReplaceExtension(".spr");
						dataSpr = GrfPath.GetData(path);
						if (dataSpr == null)
							throw new FileNotFoundException("File not found : " + path);
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
				}
			}

			Spr = new Spr(dataSpr);
			Act = new Act(dataAct, Spr);
		}
	}
}