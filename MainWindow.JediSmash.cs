using System;
using System.Collections.Generic;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kinect.Replay.Replay.Color;
using Kinect.Replay.Replay.Skeletons;
using Microsoft.Kinect;

namespace Kinect9.JediSmash
{
	public partial class MainWindow
	{
		private const int SabrePositionCount = 20;
		private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;
		private Skeleton[] _skeletons;
		private List<double> _previousSabre1PositionX, _previousSabre2PositionX;
		private int _player1Strength, _player2Strength, _player1Wins, _player2Wins;
		private DateTime _player1HitTime, _player2HitTime;
		private bool _gameMode, _hulkMode;
		private SpeechRecognizer _speechRecognizer;
		private readonly List<String> _phrases = new List<string> { "hulk", "smash" };
		private DispatcherTimer _smashAnimationTimer;
		private Dictionary<DateTime, string> _speechQueue; 

		public int Player1Strength
		{
			get { return _player1Strength; }
			set
			{
				if (value.Equals(_player1Strength)) return;
				_player1Strength = value;
				PropertyChanged.Raise(() => Player1Strength);
			}
		}

		public int Player2Strength
		{
			get { return _player2Strength; }
			set
			{
				if (value.Equals(_player2Strength)) return;
				_player2Strength = value;
				PropertyChanged.Raise(() => Player2Strength);
			}
		}

		public int Player1Wins
		{
			get { return _player1Wins; }
			set
			{
				if (value.Equals(_player1Wins)) return;
				_player1Wins = value;
				PropertyChanged.Raise(() => Player1Wins);
			}
		}

		public int Player2Wins
		{
			get { return _player2Wins; }
			set
			{
				if (value.Equals(_player2Wins)) return;
				_player2Wins = value;
				PropertyChanged.Raise(() => Player2Wins);
			}
		}

		public bool GameMode
		{
			get { return _gameMode; }
			set
			{
				if (value.Equals(_gameMode)) return;
				_gameMode = value;
				PropertyChanged.Raise(() => GameMode);
			}
		}

		public bool HulkMode
		{
			get { return _hulkMode; }
			set
			{
				if (value.Equals(_hulkMode)) return;
				_hulkMode = value;
				PropertyChanged.Raise(() => HulkMode);
			}
		}

		private void Initialize()
		{
			if (_kinectSensor == null)
				return;
			_kinectSensor.AllFramesReady += KinectSensorAllFramesReady;
			_kinectSensor.ColorStream.Enable();
			_kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
			_kinectSensor.SkeletonStream.Enable(new TransformSmoothParameters
																{
																	Correction = 0.5f,
																	JitterRadius = 0.05f,
																	MaxDeviationRadius = 0.05f,
																	Prediction = 0.5f,
																	Smoothing = 0.5f
																});

			Setup();

			_kinectSensor.Start();
            _kinectSensor.AudioSource.EchoCancellationMode = EchoCancellationMode.CancellationAndSuppression;
			_kinectSensor.AudioSource.Start();
			Message = "Kinect connected";
			KinectPresent = true;
		}

		private void Setup(string wavFilePath = null)
		{
            _speechQueue = new Dictionary<DateTime, string>();
			_speechRecognizer = new SpeechRecognizer(_phrases,wavFilePath);
			_speechRecognizer.SpeechRecognized += SpeechRecognized;
			_smashAnimationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 3) };
			_smashAnimationTimer.Tick += PlaySmashAnimation;
			_previousSabre1PositionX = new List<double>();
			_previousSabre2PositionX = new List<double>();
			ResetPlayerStrength();
			Player1Wins = Player2Wins = 0;
			GameMode = false;
			HulkMode = false;
		}

		void PlaySmashAnimation(object sender, EventArgs e)
		{
			var storyboard = (Storyboard)FindResource("Smash");
			storyboard.Begin();
			Player2Strength = 1;
			_smashAnimationTimer.Stop();
		}

		void SpeechRecognized(string speech, DateTime speechDateTime)
		{
            if(_replay!=null && speechDateTime>DateTime.Now)
            {
	            _speechQueue.Add(speechDateTime,speech);
                return;
            }

			switch (speech)
			{
				case "hulk":
					HulkMode = true;
					break;
				case "smash":
					if (HulkMode)
						HulkSmash();
					break;
			}
		}

		private void HulkSmash()
		{
			if (HulkMode)
			{
				var soundPlayer = new SoundPlayer(@"Resources\smash.wav");
				soundPlayer.Play();
				_smashAnimationTimer.Start();
			}
		}

		void KinectSensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
		{
			using (var frame = e.OpenColorImageFrame())
			{
				if (frame == null)
					return;

				ProcessColorImageFrame(frame);
			}

			using (var frame = e.OpenDepthImageFrame())
			{
				if(frame==null)
                    return;
				ProcessDepthImageFrame(frame);
			}

			using (var frame = e.OpenSkeletonFrame())
			{
				if (frame == null)
					return;
				ProcessSkeletonFrame(frame);
			}
		}

		private void ProcessDepthImageFrame(DepthImageFrame frame)
		{
			var pixels = GetColoredBytes(frame);
			if (DepthImageSource == null)
				DepthImageSource = new WriteableBitmap(frame.Width, frame.Height, 96, 96,
						PixelFormats.Bgra32, null);

			var stride = frame.Width * PixelFormats.Bgr32.BitsPerPixel / 8;
			DepthImageSource.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), pixels, stride, 0);
		}

		byte[] GetColoredBytes(DepthImageFrame frame)
		{
			var depthData = new short[frame.PixelDataLength];
			frame.CopyPixelDataTo(depthData);

			var pixels = new byte[frame.Height * frame.Width * 4];

			const int blueIndex = 0;
			const int greenIndex = 1;
			const int redIndex = 2;

			for (int depthIndex = 0, colorIndex = 0;
				 depthIndex < depthData.Length && colorIndex < pixels.Length;
				 depthIndex++, colorIndex += 4)
			{
				var player = depthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;

				var color = player == 1 ? Colors.Green : Colors.Transparent;
				pixels[colorIndex + blueIndex] = color.B;
				pixels[colorIndex + greenIndex] = color.G;
				pixels[colorIndex + redIndex] = color.R;
				if (player == 1)
					pixels[colorIndex + redIndex + 1] = 150;
				else
					pixels[colorIndex + redIndex + 1] = Colors.Transparent.A;
			}
			return pixels;
		}

		private void ProcessSkeletonFrame(ReplaySkeletonFrame skeletonFrame)
		{
			if (skeletonFrame == null)
				return;
			_skeletons = skeletonFrame.Skeletons;

			var trackedSkeleton = _skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked).ToList();

			if (!trackedSkeleton.Any())
				return;

			//Assumptions: Player 1 on left side of screen with saber in right hand, Player 2 on right side of screen with saber in left hand
			trackedSkeleton = trackedSkeleton.OrderBy(s => s.Joints[JointType.Spine].Position.X).ToList();

			DrawSaber(trackedSkeleton[0], Sabre1, FightingHand.Right, HulkMode);
			GameMode = false;

			if (trackedSkeleton.Count <= 1) return;

			GameMode = true;
			DrawSaber(trackedSkeleton[1], Sabre2, FightingHand.Left, false);
			DetectSaberCollision();
			DetectPlayerHit(trackedSkeleton[0], trackedSkeleton[1], Sabre1, Sabre2);
		}

		private void ProcessColorImageFrame(ReplayColorImageFrame colorImageFrame)
		{
            if(colorImageFrame==null)
                return;
			var pixelData = new byte[colorImageFrame.PixelDataLength];
			colorImageFrame.CopyPixelDataTo(pixelData);
			if (ColorImageSource == null)
				ColorImageSource = new WriteableBitmap(colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null);

			var stride = colorImageFrame.Width * PixelFormats.Bgr32.BitsPerPixel / 8;
			ColorImageSource.WritePixels(new Int32Rect(0, 0, colorImageFrame.Width, colorImageFrame.Height), pixelData, stride, 0);
		}

		private void DrawSaber(Skeleton skeleton, Line sabre, FightingHand fightingHand, bool inHulkMode)
		{
			Joint jointWrist, jointHand, jointElbow;

			switch (fightingHand)
			{
				case FightingHand.Left:
					jointWrist = skeleton.Joints[JointType.WristLeft];
					jointElbow = skeleton.Joints[JointType.ElbowLeft];
					jointHand = skeleton.Joints[JointType.HandLeft];
					break;
				case FightingHand.Right:
					jointWrist = skeleton.Joints[JointType.WristRight];
					jointElbow = skeleton.Joints[JointType.ElbowRight];
					jointHand = skeleton.Joints[JointType.HandRight];
					break;
				default:
					throw new ArgumentOutOfRangeException("fightingHand");
			}

			if ((jointWrist.TrackingState == JointTrackingState.NotTracked) ||
				(jointElbow.TrackingState == JointTrackingState.NotTracked) ||
				(jointHand.TrackingState == JointTrackingState.NotTracked))
				return;


			var mapper = GetCoordinateMapper();

			var wrist = mapper.MapSkeletonPointToColorPoint(jointWrist.Position, ColorFormat);
			var elbow = mapper.MapSkeletonPointToColorPoint(jointElbow.Position, ColorFormat);
			var hand = mapper.MapSkeletonPointToColorPoint(jointHand.Position, ColorFormat);

			double handAngleInDegrees;
			if (elbow.X == wrist.X)
				handAngleInDegrees = 0;
			else
			{
				var handAngleInRadian = Math.Atan((double)(elbow.Y - wrist.Y) / (wrist.X - elbow.X));
				handAngleInDegrees = handAngleInRadian * 180 / Math.PI;
			}

			if ((fightingHand == FightingHand.Right && (wrist.X < elbow.X))
				 || (fightingHand == FightingHand.Left && (wrist.X < elbow.X || wrist.Y > elbow.Y)))
				handAngleInDegrees = 180 + handAngleInDegrees;
			//Message = string.Format("{0}, {1}, {2}", elbow.Y, wrist.Y, handAngleInDegrees.ToString());	

			const int magicFudgeNumber = 45;
			double rotationAngleOffsetInDegrees;
			switch (fightingHand)
			{
				case FightingHand.Left:
					rotationAngleOffsetInDegrees = handAngleInDegrees - magicFudgeNumber;
					break;
				case FightingHand.Right:
					rotationAngleOffsetInDegrees = handAngleInDegrees + magicFudgeNumber;
					break;
				default:
					throw new ArgumentOutOfRangeException("fightingHand");
			}
			var rotationAngleOffsetInRadians = rotationAngleOffsetInDegrees * Math.PI / 180;

			sabre.X1 = ((double)wrist.X + hand.X) / 2;
			sabre.Y1 = ((double)wrist.Y + hand.Y) / 2;

			const int sabreLength = 250;
			sabre.X2 = sabre.X1 + sabreLength * Math.Cos(rotationAngleOffsetInRadians);
			sabre.Y2 = sabre.Y1 - sabreLength * Math.Sin(rotationAngleOffsetInRadians);

			PlaySabreSoundOnWave(sabre, fightingHand == FightingHand.Right ? _previousSabre1PositionX : _previousSabre2PositionX);

			if (inHulkMode)
			{
				//already have player 1 right hand info, only player 1 can be hulk
				Canvas.SetLeft(RightHandImage, hand.X - RightHandImage.ActualWidth / 2);
				Canvas.SetTop(RightHandImage, hand.Y - RightHandImage.ActualHeight / 2);
				var anticlockwiseAngle = 360 - handAngleInDegrees;
				RightHandImage.RenderTransform = new RotateTransform(anticlockwiseAngle, RightHandImage.ActualWidth / 2, RightHandImage.ActualHeight / 2);

				var headJoint = skeleton.Joints[JointType.Head];
				if (headJoint.TrackingState != JointTrackingState.NotTracked)
				{
					var head = mapper.MapSkeletonPointToColorPoint(headJoint.Position, ColorFormat);
					Canvas.SetLeft(HeadImage, head.X - HeadImage.ActualWidth / 2);
					Canvas.SetTop(HeadImage, head.Y - HeadImage.ActualHeight / 2);
				}
			}
		}

		private void PlaySabreSoundOnWave(Line sabre, List<double> previousPositions)
		{
			if (!previousPositions.Any())
			{
				previousPositions.Add(sabre.X2);
				return;
			}

			if (previousPositions.Count >= SabrePositionCount)
				previousPositions.RemoveAt(0);

			const int minimumDistanceForSoundEffect = 100;
			if (sabre.X2 < previousPositions.Last())
			{
				if (sabre.X2 < (previousPositions.Min() - minimumDistanceForSoundEffect))
					PlaySabreSound(previousPositions);
			}
			else
			{
				if (sabre.X2 > (previousPositions.Max() + minimumDistanceForSoundEffect))
					PlaySabreSound(previousPositions);
			}
			previousPositions.Add(sabre.X2);
		}

		private static void PlaySabreSound(List<double> previousPositions)
		{
			var soundPlayer = new SoundPlayer(@"Resources\lightsabre.wav");
			soundPlayer.Play();
			previousPositions.Clear();
		}

		private void DetectSaberCollision()
		{
			if (Sabre1.X2 > Sabre2.X2 &&
				 ((Sabre1.Y2 > Sabre2.Y1 && Sabre1.Y2 < Sabre2.Y2) || (Sabre1.Y2 < Sabre2.Y1 && Sabre1.Y2 > Sabre2.Y2)))
			{
				var soundPlayer = new SoundPlayer(@"Resources\clash.wav");
				soundPlayer.Play();
				_previousSabre1PositionX.Clear();
				_previousSabre2PositionX.Clear();
			}
		}

		void ResetPlayerStrength()
		{
			Player1Strength = 5;
			Player2Strength = 5;
		}

		private void DetectPlayerHit(Skeleton skeleton1, Skeleton skeleton2, Line sabre1, Line sabre2)
		{
			var player1RightShoulder = skeleton1.Joints[JointType.ShoulderRight];
			var player1Head = skeleton1.Joints[JointType.Head];

			var player2LeftShoulder = skeleton2.Joints[JointType.ShoulderLeft];
			var player2Head = skeleton2.Joints[JointType.Head];

			if (player1Head.TrackingState == JointTrackingState.NotTracked || player1RightShoulder.TrackingState == JointTrackingState.NotTracked
				|| player2Head.TrackingState == JointTrackingState.NotTracked || player2LeftShoulder.TrackingState == JointTrackingState.NotTracked)
				return;

			var coordinateMapper = GetCoordinateMapper();

			//player 1 got hit
			if (sabre2.X2 < coordinateMapper.MapSkeletonPointToColorPoint(player1RightShoulder.Position, ColorFormat).X
				 && sabre2.Y2 > coordinateMapper.MapSkeletonPointToColorPoint(player1Head.Position, ColorFormat).Y)
			{
				if (_player1HitTime.AddSeconds(1) < DateTime.Now)
				{
					Player1Strength--;
					_player1HitTime = DateTime.Now;
				}
			}

			//player 2 got hit
			if (sabre1.X2 > coordinateMapper.MapSkeletonPointToColorPoint(player2LeftShoulder.Position, ColorFormat).X
				 && sabre1.Y2 > coordinateMapper.MapSkeletonPointToColorPoint(player2Head.Position, ColorFormat).Y)
			{
				if (_player2HitTime.AddSeconds(1) < DateTime.Now)
				{
					Player2Strength--;
					_player2HitTime = DateTime.Now;
				}
			}

			if (Player1Strength <= 0 || Player2Strength <= 0)
			{
				if (Player1Strength > Player2Strength)
					Player1Wins++;
				else
					Player2Wins++;
				ResetPlayerStrength();
			}
		}

		private CoordinateMapper GetCoordinateMapper()
		{
            return _kinectSensor != null ? new CoordinateMapper(_kinectSensor) : _replay.CoordinateMapper;
		}
	}
}