using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ActEditor.ApplicationConfiguration;
using TokeiLibrary;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SplashWindow.xaml
	/// </summary>
	public partial class SplashWindow : Window {
		public SplashWindow() {
			SetValue(WpfProperties.DisableChildrenWindowsProperty, true);
			InitializeComponent();

			_version.Content = ActEditorConfiguration.PublicVersion;
			ShowInTaskbar = false;
			Topmost = true;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
		}

		public string Display {
			set { _updateMessage.Dispatch(p => p.Content = value); }
		}

		public void Terminate(int time) {
			Task.Run(() => {
				Thread.Sleep(time);
				this.Dispatch(p => p.Close());
			});
		}
	}
}