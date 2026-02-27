using System;

namespace PaletteEditor {
	public class TestMain {
		[STAThread]
		public static void Main(string[] args) {
			//var app = new App();
			//app.StartupUri = new Uri("PaletteMakerWindow.xaml", UriKind.Relative);
			//app.Run();
			new GradientColorEditDialog().ShowDialog();
		}
	}
}
