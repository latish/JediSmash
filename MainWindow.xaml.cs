using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;
using Kinect.Replay.Record;
using Kinect.Replay.Replay;
using Kinect.Replay.Replay.Skeletons;
using Microsoft.Kinect;
using Microsoft.Win32;

namespace Kinect9.JediSmash
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private KinectSensor _kinectSensor;
		private WriteableBitmap _colorImageSource;
		private string _replayFilePath;
		private bool _kinectPresent;
		private KinectReplay _replay;
		private bool _isReplaying;
		private string _message;
		private SoundPlayer _soundPlayer;
		private bool _startedAudio;
		private WriteableBitmap _depthImageSource;

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

		public bool IsReplaying
		{
			get { return _isReplaying; }
			set
			{
				if (value.Equals(_isReplaying)) return;
				_isReplaying = value;
				PropertyChanged.Raise(() => IsReplaying);
			}
		}

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

		public WriteableBitmap ColorImageSource
		{
			get { return _colorImageSource; }
			set
			{
				if (value.Equals(_colorImageSource)) return;
				_colorImageSource = value;
				PropertyChanged.Raise(() => ColorImageSource);
			}
		}

		public WriteableBitmap DepthImageSource
		{
			get { return _depthImageSource; }
			set
			{
				if (value.Equals(_depthImageSource)) return;
				_depthImageSource = value;
				PropertyChanged.Raise(() => DepthImageSource);
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

		private void ReplayFile(object sender, RoutedEventArgs e)
		{
			if (IsReplaying)
			{
				CleanupReplay();
				return;
			}
			var openFileDialog = new OpenFileDialog { Title = "Select filename", Filter = "Replay files|*.replay" };

			if (openFileDialog.ShowDialog() == true)
			{
				_replay = new KinectReplay(openFileDialog.FileName);
				_replay.AllFramesReady += ReplayAllFramesReady;
				_replay.ReplayFinished += CleanupReplay;
				Setup(_replay.AudioFilePath);
				_replay.Start();
                IsReplaying = true;
			}
		}

		private void CleanupReplay()
		{
			if (!IsReplaying) return;
			if (_soundPlayer != null && _startedAudio)
				_soundPlayer.Stop();
			_replay.AllFramesReady -= ReplayAllFramesReady;
			_replay.Stop();
			_replay.Dispose();
			_replay = null;
			IsReplaying = false;
		}

		private void ReplayAllFramesReady(ReplayAllFramesReadyEventArgs replayAllFramesReadyEventArgs)
		{
			if ((_replay.Options & KinectRecordOptions.Audio) != 0 && !_startedAudio)
			{
				_soundPlayer = new SoundPlayer(_replay.AudioFilePath);
				_soundPlayer.Play();
				_startedAudio = true;
			}
			ProcessSpeechCommands();

			var colorImageFrame = replayAllFramesReadyEventArgs.AllFrames.ColorImageFrame;
			if (colorImageFrame != null)
				ProcessColorImageFrame(colorImageFrame);

			var depthImageFrame = replayAllFramesReadyEventArgs.AllFrames.DepthImageFrame;
            if(depthImageFrame!=null)
                ProcessDepthImageFrame(depthImageFrame);

			var skeletonFrame = replayAllFramesReadyEventArgs.AllFrames.SkeletonFrame;
			if (skeletonFrame != null)
				ProcessSkeletonFrame(skeletonFrame);
		}

		private void ProcessSpeechCommands()
		{
			if (!_speechQueue.Any(s => s.Key < DateTime.Now)) return;

			var speechReady = _speechQueue.FirstOrDefault(s => s.Key < DateTime.Now);
			SpeechRecognized(speechReady.Value, speechReady.Key);
			_speechQueue.Remove(speechReady.Key);
		}
	}
}