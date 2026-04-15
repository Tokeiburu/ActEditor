using System;

namespace ActEditor.Tools.PaletteSheetGenerator {
	public enum Genders {
		Male,
		Female,
	}

	[Flags]
	public enum AllowedGenders {
		None = 0,
		Male = 1,
		Female = 2,
		Both = Male | Female,
	}

	public class SpriteResource {
		protected string _toolTip;

		public virtual string Sprite { get; set; }
		public virtual string DisplayName { get; set; }
		public virtual string ToolTip {
			get {
				if (string.IsNullOrEmpty(_toolTip))
					return DisplayName + "\r\nResource: " + Sprite;

				return _toolTip;
			}
		}

		public bool Default {
			get {
				return true;
			}
		}
	}
}
