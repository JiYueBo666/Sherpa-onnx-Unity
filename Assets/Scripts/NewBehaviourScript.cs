using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using SherpaOnnx;
using static UnityEditor.Progress;
using System.IO;
using UnityEngine.UI;


public class NewBehaviourScript : MonoBehaviour
{
    private int segmentIndex = 0;
    private string lastText = "";
    public Button button;
    public Text Text;
    public string tokens;
    public string encoder;
    public string decoder;
    public string joiner;
    public string decodingMethod = "modified_beam_search";
    public string hotwordsFile;
    private const int sampleRate = 16000;
    private int maxActivePaths = 5;
    public int enableEndPoint = 1;
    public float rule1MinTrailingSilence = 2.4F;
    public float rule2MinTrailingSilence = 2.0F;
    public float rule3MinUtteranceLength = 20.0F;
    bool isStartRecord = false;
    private AudioClip micClip;
    private int lastSamplePosition = 0;
    OnlineRecognizer recognizer;
    OnlineStream onlineStream;
    void Start()
    {
        OnlineRecognizerConfig config = new OnlineRecognizerConfig();
        config.FeatConfig.SampleRate = sampleRate;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = Path.Combine(Application.streamingAssetsPath, encoder);
        config.ModelConfig.Transducer.Decoder = Path.Combine(Application.streamingAssetsPath, decoder);
        config.ModelConfig.Transducer.Joiner = Path.Combine(Application.streamingAssetsPath, joiner);
        config.ModelConfig.Tokens = Path.Combine(Application.streamingAssetsPath, tokens);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = 1;
        config.DecodingMethod = decodingMethod;
        config.MaxActivePaths = maxActivePaths;
        config.EnableEndpoint = enableEndPoint;
        config.Rule1MinTrailingSilence = rule1MinTrailingSilence;
        config.Rule2MinTrailingSilence = rule2MinTrailingSilence;
        config.Rule3MinUtteranceLength = rule3MinUtteranceLength;
        config.HotwordsFile = Path.Combine(Application.streamingAssetsPath, hotwordsFile);
        config.HotwordsScore = 2.0f;
        config.ModelConfig.ModelingUnit = "cjkchar";

        recognizer = new OnlineRecognizer(config);
        button.onClick.AddListener(StartMicrophoneCapture);
    }

    // Update is called once per frame
    void Update()
    {
        if (!isStartRecord) return;
        int currentPosition = Microphone.GetPosition(null);
        int sampleCount = currentPosition - lastSamplePosition;
        if (sampleCount < 0)
        {
            sampleCount += micClip.samples * micClip.channels;
        }

        if (sampleCount > 0)
        {
            float[] samples = new float[sampleCount];
            micClip.GetData(samples, lastSamplePosition);
            // 将采集到的音频数据传递给识别器
            onlineStream.AcceptWaveform(micClip.frequency, samples);

            // 更新lastSamplePosition
            lastSamplePosition = currentPosition;
        }
        // 每帧更新识别器状态
        if (recognizer.IsReady(onlineStream))
        {
            recognizer.Decode(onlineStream);
        }
        var text = recognizer.GetResult(onlineStream).Text;
        Debug.Log(text);
        bool isEndpoint = recognizer.IsEndpoint(onlineStream);
        if (!string.IsNullOrWhiteSpace(text) && lastText != text)
        {
            lastText = text;
            //Debug.Log($"{segmentIndex}: {lastText}");
            Text.text = lastText;
        }
        if (isEndpoint)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                ++segmentIndex;
                StopMicrophoneCapture();
                Text buttonText = button.gameObject.transform.Find("Text").GetComponent<Text>();
                buttonText.text = "录制";
            }
            recognizer.Reset(onlineStream);
            return;
        }
    }

    public void StartMicrophoneCapture()
    {
        onlineStream = recognizer.CreateStream();
        StartCoroutine(CheckMicoPhoneInit());
    }

    IEnumerator CheckMicoPhoneInit()
    {
        string device = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        micClip = Microphone.Start(device, true, 10, 16000);
        while (!(Microphone.GetPosition(device) > 0)) { yield return null; }
        lastSamplePosition = Microphone.GetPosition(device);
        isStartRecord = true;
        Text buttonText = button.gameObject.transform.Find("Text").GetComponent<Text>();
        buttonText.text = "录制中...";

    }

    public void StopMicrophoneCapture()
    {
        isStartRecord = false;
        // 停止麦克风捕获
        Microphone.End(null);
    }
    private void OnDestroy()
    {
        recognizer.Dispose();
        if (Microphone.IsRecording(null))
            Microphone.End(null);
    }
}
