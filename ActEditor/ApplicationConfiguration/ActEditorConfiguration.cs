using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.IO;
using GRF.Image;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;

namespace ActEditor.ApplicationConfiguration {
	/// <summary>
	/// Contains all the configuration information
	/// The ConfigAsker shouldn't be used manually to store variable,
	/// make a new property instead. The properties should also always
	/// have a default value.
	/// </summary>
	public sealed class ActEditorConfiguration {
		private static ConfigAsker _configAsker;

		public static ConfigAsker ConfigAsker {
			get { return _configAsker ?? (_configAsker = new ConfigAsker(GrfPath.Combine(Configuration.ApplicationDataPath, ProgramName, "config.txt"))); }
			set { _configAsker = value; }
		}

		#region Generic settings

		public static int EncodingCodepage {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Encoding codepage]", "1252"]); }
			set { ConfigAsker["[ActEditor - Encoding codepage]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static string ProgramDataPath {
			get { return GrfPath.Combine(Configuration.ApplicationDataPath, ProgramName); }
		}

		public static ErrorLevel WarningLevel {
			get { return (ErrorLevel) Int32.Parse(ConfigAsker["[ActEditor - Warning level]", "0"]); }
			set { ConfigAsker["[ActEditor - Warning level]"] = ((int) value).ToString(CultureInfo.InvariantCulture); }
		}

		/// <summary>
		/// Gets or sets the extracting service last path.
		/// This setting name cannot be changed due to reflection.
		/// This is used by the PathRequest class.
		/// </summary>
		public static string ExtractingServiceLastPath {
			get { return ConfigAsker["[ActEditor - ExtractingService - Latest directory]", Configuration.ApplicationPath]; }
			set { ConfigAsker["[ActEditor - ExtractingService - Latest directory]"] = value; }
		}

		public static string SaveAdvancedLastPath {
			get { return ConfigAsker["[ActEditor - Save advanced path]", ExtractingServiceLastPath]; }
			set { ConfigAsker["[ActEditor - Save advanced path]"] = value; }
		}

		public static Setting ExtractSetting {
			get { return new Setting(null, typeof(ActEditorConfiguration).GetProperty("ExtractingServiceLastPath")); }
		}

		public static string AppLastPath {
			get { return ConfigAsker["[ActEditor - Application latest file name]", Configuration.ApplicationPath]; }
			set { ConfigAsker["[ActEditor - Application latest file name]"] = value; }
		}

		public static string AppLastGrfPath {
			get { return ConfigAsker["[ActEditor - Application latest grf file name]", Configuration.ApplicationPath]; }
			set { ConfigAsker["[ActEditor - Application latest grf file name]"] = value; }
		}

		#endregion

		#region Gif settings

		public static bool KeepPreviewSelectionFromActionChange {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Keep action selection]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Keep action selection]"] = value.ToString(); }
		}

		public static bool ActEditorGifUniform {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Gif - Uniform]", true.ToString()]); }
			set { ConfigAsker["[ActEditor -  Gif - Uniform]"] = value.ToString(); }
		}

		public static bool ActEditorGifHideDialog {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Gif - Hide saving dialog]", false.ToString()]); }
			set { ConfigAsker["[ActEditor - Gif - Hide saving dialog]"] = value.ToString(); }
		}

		public static float ActEditorGifDelayFactor {
			get { return float.Parse(ConfigAsker["[ActEditor - Gif - Delay factor]", "1"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[ActEditor - Gif - Delay factor]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static int ActEditorGifMargin {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Gif - Margin]", "3"]); }
			set { ConfigAsker["[ActEditor - Gif - Margin]"] = (value < 2 ? 2 : value).ToString(CultureInfo.InvariantCulture); }
		}

		public static Color ActEditorGifBackgroundColor {
			get { return new GrfColor((ConfigAsker["[ActEditor - Background color]", GrfColor.ToHex(255, 255, 255, 255)])).ToColor(); }
			set { ConfigAsker["[ActEditor - Background color]"] = GrfColor.ToHex(value.A, value.R, value.G, value.B); }
		}

		public static Color ActEditorGifGuidelinesColor {
			get { return new GrfColor((ConfigAsker["[ActEditor - Guidelines color]", GrfColor.ToHex(0, 0, 0, 0)])).ToColor(); }
			set { ConfigAsker["[ActEditor - Guidelines color]"] = GrfColor.ToHex(value.A, value.R, value.G, value.B); }
		}

		#endregion

		#region Others

		/// <summary>
		/// Xaml binding property for the background; this is
		/// to avoid crashes in the designer.
		/// </summary>
		public static Brush UIPanelPreviewBackground {
			get { return new SolidColorBrush(ActEditorBackgroundColor); }
		}

		/// <summary>
		/// Gets or sets the resources.
		/// This setting has been imported from GrfEditor. It is used
		/// by the MetaGrf class.
		/// </summary>
		public static List<string> Resources {
			get { return Methods.StringToList(ConfigAsker["[ActEditor - Resources]", ""]); }
			set { ConfigAsker["[ActEditor - Resources]"] = Methods.ListToString(value); }
		}

		#endregion

		#region Palette Editor imported settings

		public static bool PaletteEditorOpenWindowsEdits {
			get { return Boolean.Parse(ConfigAsker["[Palette editor - Open palette edits in a window]", false.ToString()]); }
			set { ConfigAsker["[Palette editor - Open palette edits in a window]"] = value.ToString(); }
		}

		#endregion

		#region Editor settings

		private static bool? _showAnchors;
		private static bool? _useAliasing;
		private static BitmapScalingMode? _mode;

		public static string ActEditorScriptRunnerScript {
			get { return ConfigAsker["[ActEditor - Script Runner - Latest script]", "// Script example, for a complete list of available methods,__%LineBreak%// click on the 'Help' button__%LineBreak%foreach (var selectedLayerIndex in selectedLayerIndexes) {__%LineBreak%	var layer = act[selectedActionIndex, selectedFrameIndex, selectedLayerIndex];__%LineBreak%	layer.Translate(-10, 0);__%LineBreak%	layer.Rotate(15);__%LineBreak%}__%LineBreak%__%LineBreak%foreach (var action in act) {__%LineBreak%	foreach (var frame in action) {__%LineBreak%		foreach (var layer in frame) {__%LineBreak%			layer.OffsetX = 2 * layer.OffsetX;__%LineBreak%			layer.ScaleX *= 2f;__%LineBreak%			layer.Scale(1f, 2f);__%LineBreak%		}__%LineBreak%	}__%LineBreak%}__%LineBreak%"]; }
			set { ConfigAsker["[ActEditor - Script Runner - Latest script]"] = value; }
		}

		public static bool ActEditorScriptRunnerAutocomplete {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Script runner - Autocomplete]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Script runner - Autocomplete]"] = value.ToString(); }
		}

		public static bool ActEditorCopyFromCurrentFrame {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Copy from current frame]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Copy from current frame]"] = value.ToString(); }
		}

		public static bool ShowAnchors {
			get {
				if (_showAnchors == null)
					_showAnchors = Boolean.Parse(ConfigAsker["[ActEditor - Show anchors]", false.ToString()]);

				return _showAnchors.Value;
			}
			set {
				ConfigAsker["[ActEditor - Show anchors]"] = value.ToString();
				_showAnchors = value;
			}
		}

		public static bool UseAliasing {
			get {
				if (_useAliasing == null)
					_useAliasing = Boolean.Parse(ConfigAsker["[ActEditor - Use aliasing]", false.ToString()]);

				return _useAliasing.Value;
			}
			set {
				ConfigAsker["[ActEditor - Use aliasing]"] = value.ToString();
				_useAliasing = value;
			}
		}

		public static bool UseAccurateFrameInterval {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - UseAccurateFrameInterval]", false.ToString()]); }
			set { ConfigAsker["[ActEditor - UseAccurateFrameInterval]"] = value.ToString(); }
		}

		public static bool ReverseAnchor {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - ReverseAnchor]", false.ToString()]); }
			set { ConfigAsker["[ActEditor - ReverseAnchor]"] = value.ToString(); }
		}

		public static bool ActEditorPlaySound {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Play sounds]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Play sounds]"] = value.ToString(); }
		}

		public static bool ReopenLatestFile {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Open latest file on startup]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Open latest file on startup]"] = value.ToString(); }
		}

		public static bool ActEditorRefreshLayerEditor {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Refresh layer editor]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Refresh layer editor]"] = value.ToString(); }
		}

		public static bool ShellAssociateAct {
			get { return Boolean.Parse(ConfigAsker["[Application - Shell associate - Act]", false.ToString()]); }
			set { ConfigAsker["[Application - Shell associate - Act]"] = value.ToString(); }
		}

		public static bool ActEditorGridLineHVisible {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Grid line horizontal visible]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Grid line horizontal visible]"] = value.ToString(); }
		}

		public static bool ActEditorGridLineVVisible {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Grid line vertical visible]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Grid line vertical visible]"] = value.ToString(); }
		}

		public static bool ActEditorExportCurrentSprite {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - ActEditorExportCurrentSprite]", false.ToString()]); }
			set { ConfigAsker["[ActEditor - ActEditorExportCurrentSprite]"] = value.ToString(); }
		}

		public static bool ActEditorExportCurrentFolder {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - ActEditorExportCurrentFolder]", false.ToString()]); }
			set { ConfigAsker["[ActEditor - ActEditorExportCurrentFolder]"] = value.ToString(); }
		}

		public static Color ActEditorBackgroundColor {
			get { return new GrfColor((ConfigAsker["[ActEditor - Background preview color]", GrfColor.ToHex(150, 0, 0, 0)])).ToColor(); }
			set { ConfigAsker["[ActEditor - Background preview color]"] = GrfColor.ToHex(value.A, value.R, value.G, value.B); }
		}

		public static Color ActEditorSpriteBackgroundColor {
			get { return new GrfColor((ConfigAsker["[ActEditor - Background sprite preview color]", GrfColor.ToHex(150, 0, 0, 0)])).ToColor(); }
			set { ConfigAsker["[ActEditor - Background sprite preview color]"] = GrfColor.ToHex(value.A, value.R, value.G, value.B); }
		}

		public class QuickSetting<T> {
			private string _propertyName;
			private string _defaultValue;
			public Func<string, T> ConverterTo;
			public Func<T, string> ConverterFrom;
			private T _cached;

			public delegate void PropertyChangedEventHandler();

			public event PropertyChangedEventHandler PropertyChanged;

			public QuickSetting(string propertyName, string defaultValue, Func<string, T> getter, Func<T, string> setter) {
				_propertyName = propertyName;
				_defaultValue = defaultValue;
				ConverterTo = getter;
				ConverterFrom = setter;
			}

			public T Get() {
				if (_cached == null) {
					_cached = ConverterTo(ConfigAsker[_propertyName, _defaultValue]);
				}

				return _cached;
			}

			public void Set(T value) {
				ConfigAsker[_propertyName] = ConverterFrom(value);
				_cached = value;
				PropertyChanged?.Invoke();
			}

			public string GetDefaultString() {
				return _defaultValue;
			}
		}

		public static QuickSetting<GrfColor> ActEditorSpriteSelectionBorder = new QuickSetting<GrfColor>("[ActEditor - Selected sprite border color]", GrfColor.ToHex(255, 255, 0, 0), v => new GrfColor(v), v => v.ToHexString());
		public static QuickSetting<GrfColor> ActEditorSpriteSelectionBorderOverlay = new QuickSetting<GrfColor>("[ActEditor - Selected sprite overlay color]", GrfColor.ToHex(0, 255, 255, 255), v => new GrfColor(v), v => v.ToHexString());
		public static QuickSetting<GrfColor> ActEditorSelectionBorder = new QuickSetting<GrfColor>("[ActEditor - Selection border color]", GrfColor.ToHex(255, 0, 0, 255), v => new GrfColor(v), v => v.ToHexString());
		public static QuickSetting<GrfColor> ActEditorSelectionBorderOverlay = new QuickSetting<GrfColor>("[ActEditor - Selection overlay color]", GrfColor.ToHex(50, 128, 128, 255), v => new GrfColor(v), v => v.ToHexString());
		public static QuickSetting<GrfColor> ActEditorAnchorColor = new QuickSetting<GrfColor>("[ActEditor - Anchor color]", GrfColor.ToHex(200, 255, 255, 0), v => new GrfColor(v), v => v.ToHexString());
		public static QuickSetting<GrfColor> ActEditorGridLineHorizontal = new QuickSetting<GrfColor>("[ActEditor - Grid line horizontal color]", GrfColor.ToHex(255, 0, 0, 0), v => new GrfColor(v), v => v.ToHexString());
		public static QuickSetting<GrfColor> ActEditorGridLineVertical = new QuickSetting<GrfColor>("[ActEditor - Grid line vertical color]", GrfColor.ToHex(255, 0, 0, 0), v => new GrfColor(v), v => v.ToHexString());

		public static float ActEditorZoomInMultiplier {
			get { return float.Parse(ConfigAsker["[ActEditor - Zoom in multiplier]", "1"]); }
			set { ConfigAsker["[ActEditor - Zoom in multiplier]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static BitmapScalingMode ActEditorScalingMode {
			get {
				if (_mode != null) {
					return _mode.Value;
				}

				var value = (BitmapScalingMode) Enum.Parse(typeof (BitmapScalingMode), ConfigAsker["[ActEditor - Scale mode]", BitmapScalingMode.NearestNeighbor.ToString()], true);
				_mode = value;
				return value;
			}
			set {
				ConfigAsker["[ActEditor - Scale mode]"] = value.ToString();
				_mode = value;
			}
		}

		public static bool SaveEditorPosition {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - SaveEditorPosition]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - SaveEditorPosition]"] = value.ToString(); }
		}

		public static string EditorSavedPositions {
			get { return ConfigAsker["[ActEditor - EditorSavedPositions]", ""]; }
			set { ConfigAsker["[ActEditor - EditorSavedPositions]"] = value; }
		}

		public static string ActEditorGarmentPaths {
			get { return ConfigAsker["[ActEditor - Garment paths]", ""]; }
			set { ConfigAsker["[ActEditor - Garment paths]"] = value; }
		}

		public static bool ActEditorGarmentCopySpr {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - ActEditorGarmentCopySpr]", false.ToString()]); }
			set { ConfigAsker["[ActEditor - ActEditorGarmentCopySpr]"] = value.ToString(); }
		}

		public static bool ActEditorGarmentGuessAnchor {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - ActEditorGarmentGuessAnchor]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - ActEditorGarmentGuessAnchor]"] = value.ToString(); }
		}

		public static string ActEditorGarmentSpriteName {
			get { return ConfigAsker["[ActEditor - ActEditorGarmentSpriteName]", "Custom_Wing"]; }
			set { ConfigAsker["[ActEditor - ActEditorGarmentSpriteName]"] = value; }
		}

		public static string GarmentSavePath {
			get { return ConfigAsker["[ActEditor - Garment save path]", Configuration.ApplicationPath]; }
			set { ConfigAsker["[ActEditor - Garment save path]"] = value; }
		}

		public static bool PreviewSheetHeadAffected {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetHeadAffected]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetHeadAffected]"] = value.ToString(); }
		}

		public static bool PreviewSheetAutomaticPath {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetAutomaticPath]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetAutomaticPath]"] = value.ToString(); }
		}

		public static bool PreviewSheetBodyAffected {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetBodyAffected]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetBodyAffected]"] = value.ToString(); }
		}

		public static bool PreviewSheetShowBody {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetShowBody]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetShowBody]"] = value.ToString(); }
		}

		public static bool PreviewSheetTransparentBackground {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - Transparent background]", "false"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - Transparent background]"] = value.ToString(); }
		}

		public static bool PreviewSheetShowShadow {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - Show shadow]", "false"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - Show shadow]"] = value.ToString(); }
		}

		public static bool PreviewSheetShowHead {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetShowHead]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetShowHead]"] = value.ToString(); }
		}

		public static bool PreviewSheetUsePredefinedPalettePath {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetUsePredefinedPalettePath]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetUsePredefinedPalettePath]"] = value.ToString(); }
		}

		public static string PreviewSheetPredefinedPalettePath {
			get { return ConfigAsker["[ActEditor - PreviewSheetPredefinedPalettePath]", "³ëºñ½ºÆ÷¸µ"]; }
			set { ConfigAsker["[ActEditor - PreviewSheetPredefinedPalettePath]"] = value; }
		}

		public static bool PreviewSheetShowPalIndex {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetShowPalIndex]", "true"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetShowPalIndex]"] = value.ToString(); }
		}

		public static bool PreviewSheetUseOldConfig {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetUseOldConfig]", "false"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetUseOldConfig]"] = value.ToString(); }
		}

		public static string PreviewSheetIndexes {
			get { return ConfigAsker["[ActEditor - Preview sheet - Indexes]", "0-199"]; }
			set { ConfigAsker["[ActEditor - Preview sheet - Indexes]"] = value; }
		}

		public static string PreviewSheetLastSelectedJob {
			get { return ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedJob]", ""]; }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedJob]"] = value; }
		}

		public static int PreviewSheetLastSelectedJobList {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedJobList]", "0"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedJobList]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static string PreviewSheetLastSelectedHead {
			get { return ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedHead]", ""]; }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedHead]"] = value; }
		}

		public static int PreviewSheetLastSelectedHeadList {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedHeadList]", "0"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewSheetLastSelectedHeadList]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int PreviewSheetMaxPerLine {
			get { return int.Parse(ConfigAsker["[ActEditor - Preview sheet - Max per line]", "10"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - Max per line]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static string PreviewSheetPalPath {
			get { return ConfigAsker["[ActEditor - Preview sheet - Pal path]", "C:\\YourRO.grf"]; }
			set { ConfigAsker["[ActEditor - Preview sheet - Pal path]"] = value; }
		}

		public static int PreviewSheetActionIndex {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Preview sheet - Action index]", "0"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - Action index]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int PreviewFontType {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Preview sheet - PreviewFontType]", "2"]); }
			set { ConfigAsker["[ActEditor - Preview sheet - PreviewFontType]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static bool ShowErrorRleDowngrade {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - ShowErrorRleDowngrade]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - ShowErrorRleDowngrade]"] = value.ToString(); }
		}

		#endregion

		#region GrfShell
		public static string GrfShellLatest {
			get { return ConfigAsker["[ActEditor - GrfShell - Latest file]", ""]; }
			set { ConfigAsker["[ActEditor - GrfShell - Latest file]"] = value; }
		}
		
		#endregion

		#region Interpolation
		public static bool InterpolateOffsets {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Interpolation - Offsets]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Interpolation - Offsets]"] = value.ToString(); }
		}

		public static bool InterpolateScale {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Interpolation - Scale]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Interpolation - Scale]"] = value.ToString(); }
		}

		public static bool InterpolateAngle {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Interpolation - Angle]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Interpolation - Angle]"] = value.ToString(); }
		}

		public static bool InterpolateColor {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Interpolation - Color]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Interpolation - Color]"] = value.ToString(); }
		}

		public static bool InterpolateMirror {
			get { return Boolean.Parse(ConfigAsker["[ActEditor - Interpolation - Mirror]", true.ToString()]); }
			set { ConfigAsker["[ActEditor - Interpolation - Mirror]"] = value.ToString(); }
		}

		public static int InterpolateEase {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Interpolation - Ease]", "0"]); }
			set { ConfigAsker["[ActEditor - Interpolation - Ease]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int BrushSize {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Brush size]", "5"]); }
			set { ConfigAsker["[ActEditor - Brush size]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static double InterpolateTolerance {
			get { return FormatConverters.SingleConverter(ConfigAsker["[ActEditor - Interpolation - Tolerance]", "0.9"]); }
			set { ConfigAsker["[ActEditor - Interpolation - Tolerance]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int InterpolateRange {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Interpolation - Range]", "20"]); }
			set { ConfigAsker["[ActEditor - Interpolation - Range]"] = value.ToString(CultureInfo.InvariantCulture); }
		}
		#endregion

		#region Sprite Editor

		// These settings have been imported from Sprite Editor
		public static bool UseDithering {
			get { return Boolean.Parse(ConfigAsker["[Sprite Editor - Use dithering]", false.ToString()]); }
			set { ConfigAsker["[Sprite Editor - Use dithering]"] = value.ToString(); }
		}

		public static string BackgroundPath {
			get { return ConfigAsker["[ActEditor - Script - Background path]", Configuration.ApplicationPath]; }
			set { ConfigAsker["[ActEditor - Script - Background path]"] = value; }
		}

		public static bool UseTgaImages {
			get { return Boolean.Parse(ConfigAsker["[Sprite Editor - Use TGA images]", false.ToString()]); }
			set { ConfigAsker["[Sprite Editor - Use TGA images]"] = value.ToString(); }
		}

		public static int TransparencyMode {
			get { return Int32.Parse(ConfigAsker["[Sprite Editor - Transparency mode]", "1"]); }
			set { ConfigAsker["[Sprite Editor - Transparency mode]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int FormatConflictOption {
			get { return Int32.Parse(ConfigAsker["[Sprite Editor - Last format conflict option]", "2"]); }
			set { ConfigAsker["[Sprite Editor - Last format conflict option]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		private static ObservableDictionary<string, string> _remapper;

		public static ObservableDictionary<string, string> Remapper {
			get {
				if (_remapper != null)
					return _remapper;

				var value = ConfigAsker["[Act Editor - Remapper]", ""];

				var gestures = new ObservableDictionary<string, string>();
				string[] groups = value.Split('%');

				foreach (var sub in groups) {
					if (sub.Length < 1)
						continue;

					string[] values = sub.Split('|');

					if (values.Length < 2)
						continue;

					gestures[values[0]] = values[1];
				}

				_remapper = gestures;

				_remapper.CollectionChanged += delegate {
					StringBuilder b = new StringBuilder();

					foreach (var keyPair in _remapper) {
						b.Append(keyPair.Key);
						b.Append("|");
						b.Append(keyPair.Value);
						b.Append("%");
					}

					ConfigAsker["[Act Editor - Remapper]"] = b.ToString();
				};

				return gestures;
			}
		}

		#endregion

		#region Program's internal configuration and information

		public static string PublicVersion => "1.3.2";
		public static string Author => "Tokeiburu";
		public static string ProgramName => "Act Editor";
		public static string RealVersion => Assembly.GetEntryAssembly().GetName().Version.ToString();

		public static int PatchId {
			get { return Int32.Parse(ConfigAsker["[ActEditor - Patch ID]", "0"]); }
			set { ConfigAsker["[ActEditor - Patch ID]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int ThemeIndex {
			get { return Int32.Parse(ConfigAsker["[ActEditor - ThemeIndex]", "1"]); }
			set { ConfigAsker["[ActEditor - ThemeIndex]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static string StyleTheme {
			get { return ConfigAsker["[ActEditor - StyleTheme]", ""]; }
			set { ConfigAsker["[ActEditor - StyleTheme]"] = value; }
		}

		#endregion

		#region Property binders

		// The binder must be instantiated at least once to be registered by the Binder class.
		internal static BinderBase QcsB = new QuickColorSelectorBinder();

		public class QuickColorSelectorBinder : BinderAbstract<QuickColorSelector, Color> {
			/// <summary>
			/// Binds the specified UIElement with a setting.
			/// </summary>
			/// <param name="element">The UIElement.</param>
			/// <param name="get">The get method.</param>
			/// <param name="set">The set method.</param>
			/// <param name="extra">The action to take upon setting the binding.</param>
			/// <param name="execute">Executes the action after this method.</param>
			public override void Bind(QuickColorSelector element, Func<Color> get, Action<Color> set, Action extra, bool execute) {
				element.Color = get();
				element.Init(Configuration.ConfigAsker.RetrieveSetting(() => get()));

				element.ColorChanged += delegate(object sender, Color value) {
					set(value);

					if (extra != null)
						extra();
				};

				if (execute) {
					if (extra != null)
						extra();
				}
			}
		}

		#endregion
	}
}