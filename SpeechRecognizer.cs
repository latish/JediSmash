using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Speech.Recognition;

namespace Kinect9.JediSmash
{
	class SpeechRecognizer
	{
		private readonly List<string> _phrases;
		private readonly SpeechRecognitionEngine _speechRecognitionEngine;
		public event Action<string, DateTime> SpeechRecognized;

		public SpeechRecognizer(List<string> phrases, string wavFilePath)
		{
			_phrases = phrases;
			_speechRecognitionEngine = CreateSpeechRecognizer();
			if (string.IsNullOrEmpty(wavFilePath))
				_speechRecognitionEngine.SetInputToDefaultAudioDevice();
			else
				_speechRecognitionEngine.SetInputToWaveFile(wavFilePath);
			_speechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);
		}

		private SpeechRecognitionEngine CreateSpeechRecognizer()
		{
			var recognizerInfo = GetKinectRecognizer();

			var speechRecognitionEngine = new SpeechRecognitionEngine(recognizerInfo.Id);

			var grammar = new Choices(_phrases.ToArray());

			var gb = new GrammarBuilder { Culture = recognizerInfo.Culture };
			gb.Append(grammar);

			var g = new Grammar(gb);

			speechRecognitionEngine.LoadGrammar(g);
			speechRecognitionEngine.SpeechRecognized += SreSpeechRecognized;

			return speechRecognitionEngine;
		}

		private void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
		{
			if (e.Result.Confidence < 0.7 )
				return;

			var phraseDateTime = e.Result.Audio.StartTime.Add(e.Result.Audio.AudioPosition);

			if (_phrases.Contains(e.Result.Text))
				SpeechRecognized(e.Result.Text,phraseDateTime );
		}

		private static RecognizerInfo GetKinectRecognizer()
		{
			Func<RecognizerInfo, bool> matchingFunc = r =>
				                                          {
					                                          string value;
					                                          r.AdditionalInfo.TryGetValue("Kinect", out value);
					                                          return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
				                                          };
			return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
		}
	}
}