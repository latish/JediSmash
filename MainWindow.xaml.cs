using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Kinect.Toolbox.Record;
using Microsoft.Kinect;
using Microsoft.Win32;

namespace Kinect9.JediSmash
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private KinectSensor _kinectSensor;
		private string _message;

		public MainWindow()
		{
			InitializeComponent();
		}

		public string Message
		{
			get { return _message; }
			set
			{
				if (value.Equals(_message)) return;
				_message = value;
				PropertyChanged.Raise(() => Message);
			}
		}

		private WriteableBitmap _imageSource;
		private string _replayFilePath;
		private bool _kinectPresent;

		public bool KinectPresent
		{
			get { return _kinectPresent; }
			set
			{
				if (value.Equals(_kinectPresent)) return;
				_kinectPresent = value;
				PropertyChanged.Raise(() => KinectPresent);
			}
		}

		public WriteableBitmap ImageSource
		{
			get { return _imageSource; }
			set
			{
				if (value.Equals(_imageSource)) return;
				_imageSource = value;
				PropertyChanged.Raise(() => ImageSource);
			}
		}

		public string ReplayFilePath
		{
			get { return _replayFilePath; }
			set
			{
				if (value.Equals(_replayFilePath)) return;
				_replayFilePath = value;
				PropertyChanged.Raise(() => ReplayFilePath);
			}
		}

		private void MainWindowLoaded(object sender, RoutedEventArgs e)
		{
			try
			{
				KinectSensor.KinectSensors.StatusChanged += KinectSensorsStatusChanged;

				_kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(sensor => sensor.Status == KinectStatus.Connected);
				if (_kinectSensor == null)
				{
					Message = "No Kinect found on startup";
					KinectPresent = false;
				}
				else
					Initialize();
			}
			catch (Exception ex)
			{
				Message = ex.Message;
			}
		}

		void KinectSensorsStatusChanged(object sender, StatusChangedEventArgs e)
		{
			switch (e.Status)
			{
				case KinectStatus.Disconnected:
					if (_kinectSensor == e.Sensor)
					{
						Clean();
						Message = "Kinect disconnected";
					}
					break;
				case KinectStatus.Connected:
					_kinectSensor = e.Sensor;
					Initialize();
					break;
				case KinectStatus.NotPowered:
					Message = "Kinect is not powered";
					Clean();
					break;
				case KinectStatus.NotReady:
					Message = "Kinect is not ready";
					break;
				case KinectStatus.Initializing:
					Message = "Initializing";
					break;
				default:
					Message = string.Concat("Status: ", e.Status);
					break;
			}
		}

		private void Clean()
		{
			KinectPresent = false;
			if (_kinectSensor == null)
				return;

			if (_kinectSensor.AudioSource != null)
				_kinectSensor.AudioSource.Stop();
			if (_kinectSensor.IsRunning)
				_kinectSensor.Stop();
			_kinectSensor.AllFramesReady -= KinectSensorAllFramesReady;
			_kinectSensor.Dispose();
			_kinectSensor = null;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			var handler = PropertyChanged;
			if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
		}

		private void BrowseReplayFile(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog { Title = "Select filename", Filter = "Replay files|*.replay" };

			if (openFileDialog.ShowDialog() == true)
				ReplayFilePath = openFileDialog.FileName;
		}

		private void ReplayFile(object sender, RoutedEventArgs e)
		{
			if (!File.Exists(ReplayFilePath)) return;
			if (_replay != null)
			{
				_replay.AllFramesReady -= ReplayAllFramesReady;
				_replay.Stop();
			}
            Setup();
			Stream recordStream = File.OpenRead(ReplayFilePath);

			_replay = new KinectReplay(recordStream);

			_replay.AllFramesReady += ReplayAllFramesReady;

			_replay.Start();
		}

		private void ReplayAllFramesReady(object sender, ReplayAllFramesReadyEventArgs replayAllFramesReadyEventArgs)
		{
			var colorImageFrame = replayAllFramesReadyEventArgs.AllFrames.ColorImageFrame;
            if(colorImageFrame!=null)
                ProcessColorImageFrame(colorImageFrame);

			var skeletonFrame = replayAllFramesReadyEventArgs.AllFrames.SkeletonFrame;
            if(skeletonFrame!=null)
                ProcessSkeletonFrame(skeletonFrame);
		}
	}
}