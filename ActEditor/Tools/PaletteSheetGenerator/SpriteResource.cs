using System;
using GRF.Core;
using GRF.FileFormats.ActFormat;

namespace ActEditor.Tools.PaletteSheetGenerator {
	public class SpriteResource {
		private string _toolTip;
		private readonly Func<GrfHolder, Act> _methodAct;
		public string Gender { get; set; }
		public string DisplayName { get; set; }
		public string PalettePath { get; set; }
		public string LoadPath { get; set; }
		public bool IsCostume { get; set; }

		public string ToolTip {
			get {
				if (string.IsNullOrEmpty(_toolTip))
					return DisplayName + "\r\nResource: " + SpriteName + "\r\nPalette: " + PalettePath;

				return _toolTip;
			}
		}

		public SpriteResource(Func<GrfHolder, Act> methodAct, string gender, string spriteName, string displayName, string palettePath, bool isCostume, string toolTip = "") {
			_methodAct = methodAct;
			Gender = gender;
			SpriteName = spriteName;
			DisplayName = displayName;
			PalettePath = palettePath;
			IsCostume = isCostume;
			_toolTip = toolTip;
		}

		public string SpriteName { get; set; }

		public override string ToString() {
			return DisplayName;
		}

		public Act GetAct(GrfHolder grf) {
			try {
				return _methodAct(grf);
			}
			catch {
				return null;
			}
		}

		public bool Default {
			get {
				return true;
			}
		}
	}
}
