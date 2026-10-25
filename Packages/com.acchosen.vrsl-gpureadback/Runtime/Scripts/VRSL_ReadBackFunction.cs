using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Object = UnityEngine.Object;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
using VRSL.EditorScripts;
#endif

namespace VRSL.GPUReadBack
{
    public enum ReadType
    {
        Animation,
        Particles,
        Event,
        ObjectToggle,
        Data
    }

    public enum AnimationParamType
    {
        Bool,
        Int,
        Float,
        Trigger
    }

    public enum ValueOutputMethod
    {
        Value,
        Range
    }
    
    public enum DataOutputType
    {
        Bool,
        Int,
        Float,
        String
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VRSL_ReadBackFunction : UdonSharpBehaviour
    {
        [SerializeField] private float universalSmoothingStrength = 0.5f;
        public VRSL_GPUReadBack reader;
        public int size = 10;

        [SerializeField]
        private Animator[] animators = new Animator[512];
        [SerializeField, HideInInspector] public int[] dmxChannelLength = new int[512];
        [SerializeField, HideInInspector] public int[] dmxUnviverses = new int[512];
        [SerializeField, HideInInspector] public int[] dmxChannels = new int[512];
        [SerializeField] private float[] dmxRawData = new float[512];
        private float[] previousdmxRawData = new float[512];
        private float[] dmxRawDataVelocity = new float[512];
        private float[] floatDataVelocity = new float[512];
        public UdonBehaviour[] targetFunctions = new UdonBehaviour[512];
        [SerializeField] private bool[] eventFireReset = new bool[512];
        [SerializeField] private int[] eventFireResetThreshold = new int[512];
        [SerializeField] private bool[] eventSpamToggle = new bool[512];
        [SerializeField] private float[] eventFireTimer = new float[512];
        [SerializeField] private float[] eventSpamDelay = new float[512];
        public string[] eventNames = new string[512];
        [SerializeField] private int[] eventCallThreshold = new int[512];
        [SerializeField] private ParticleSystem[] particlesSystems = new ParticleSystem[512];
        [SerializeField] private int[] particlesSystemsThreshold = new int[512];
        [SerializeField] private bool[] particlesSystemsOneShotToggle = new bool[512];
        [SerializeField] private bool[] particlesSystemsOneShotReset = new bool[512];
        [SerializeField] private int[] particlesSystemsOneShotResetThreshold = new int[512];
        [SerializeField] private ReadType[] readTypes = new ReadType[512];
        [SerializeField] private AnimationParamType[] parameterTypes = new AnimationParamType[512];
        [SerializeField] private bool[] animationBoolParams = new bool[512];
        [SerializeField] private bool[] floatSmoothingToggle = new bool[512];
        [SerializeField] private ValueOutputMethod[] animationRangeToggle = new ValueOutputMethod[512];
        [SerializeField] private int[] animationBoolParamsThreshold = new int[512];
        [SerializeField] private float[] animationFloatParams = new float[512];
        [SerializeField] private int[] animationFloatParamsThreshold = new int[512];
        [SerializeField] private Vector2[] animationFloatParamsRange = new Vector2[512];
        [SerializeField] private int[] animationIntParams = new int[512];
        [SerializeField] private int[] animationIntParamsThreshold = new int[512];
        [SerializeField] private int[] animationIntParamsRangeX = new int[512];
        [SerializeField] private int[] animationIntParamsRangeY = new int[512];
        [SerializeField] private string[] animationParamNames = new string[512];
        // [SerializeField] private bool[]animationTriggerToggle = new bool[512];
        [SerializeField] private bool[] animationTriggerReset = new bool[512];
        [SerializeField] private int[] animationTriggerResetThreshold = new int[512];
        [SerializeField] private int[] animationTriggerParamsThreshold = new int[512];
    /////////////////////////////////////////////////////////////////////////////////////////////

        [SerializeField] private DataOutputType[] dataTypes = new DataOutputType[512];
        [SerializeField] private bool[] dataBools = new bool[512];
        [SerializeField] private bool[] dataBoolsPrevious = new bool[512];
        [SerializeField] private int[]dataBoolsThreshold = new int[512];
        [SerializeField] private int[] dataInts = new int[512];
        [SerializeField] private int[] dataIntsPrevious = new int[512];
        [SerializeField] private int[] dataIntThreshold = new int[512];
        [SerializeField] private int[] dataIntRangeX = new int[512];
        [SerializeField] private int[] dataIntRangeY = new int[512];
        [SerializeField] private float[] dataFloats = new float[512];
        [SerializeField] private float[] dataFloatsPrevious = new float[512];
        [SerializeField] private int[] dataFloatThreshold = new int[512];
        [SerializeField] private int[] dataInterpolationTolerance = new int[512];
        [SerializeField] private Vector2[] dataFloatRange = new Vector2[512];
        [SerializeField] private bool[] dataUseEvent = new bool[512];
        [SerializeField] private GameObject[] toggleGameobjects = new GameObject[512];
        [SerializeField] private bool[] invertToggle = new bool[512];
    /////////////////////////////////////////////////////////////////////////////////////////////////////
        public VRSL_ReadBackFunction_StringList[]dataStringList = new VRSL_ReadBackFunction_StringList[512];
        [SerializeField] private int[] dataStringRangeX = new int[512]; 
        [SerializeField] private int[] dataStringRangeY = new int[512]; 
        
        private int[] dataStringPreviousIndex = new int[512];
        int width, height;
        int offsetUpBy4Pixels, rowSize, rowSizeInv, totalNumOfPixels;

        [SerializeField] private bool[] dataFoldout = new bool[512];

        public string[] dataStringActive = new string[512];
        public bool[] dataBoolActive = new bool[512];
        public int[] dataIntActive = new int[512];
        public float[] dataFloatActive = new float[512];
        private float[] previousDataFloatActive = new float[512];
        [SerializeField] private float[] smoothingAmount = new float[512];
        [SerializeField] private float[] floatSmoothingMultiplier = new float[512];
        [SerializeField] private float smoothingSpeed = 0.1f;

        void Start()
        {
            width = (int)universalSmoothingStrength;
            width = reader.texture.width;
            height = reader.texture.height;

            offsetUpBy4Pixels = width * ( (int) Mathf.Clamp(((reader.dmxPixelSize/2) - 1), 1, 10));
            rowSize = width / reader.dmxPixelSize;
            rowSizeInv = reader.dmxPixelSize * width;
            totalNumOfPixels = width * height;
            //hardoffset = reader.dmxPixelSize == 2 ? 0 : 1;
        }
        
        int IntRemap(int source, int sourceFrom, int sourceTo, int targetFrom, int targetTo)
        {
            return (int) Mathf.Round((float)targetFrom + ((float)source-(float)sourceFrom)*((float)targetTo-(float)targetFrom)/((float)sourceTo-(float)sourceFrom));
        }
        
        float FloatRemap(float source, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
        {
            return targetFrom + (source-sourceFrom)*(targetTo-targetFrom)/(sourceTo-sourceFrom);
        }

        int GetIndex(int startindex, int i, out int index, out int xOffset, out int yOffset)
        {
            xOffset = ((dmxChannels[i]-1) % (rowSize)) * reader.dmxPixelSize;
            yOffset = (((dmxChannels[i]-1) / (rowSize)) * (rowSizeInv));
            index =  Mathf.Clamp(startindex + yOffset + xOffset,0, totalNumOfPixels-1);
            return index;
        }
        
        public bool _GetBoolData(int index)
        {
            return dataBoolActive[index];
        }
        
        public int _GetIntData(int index)
        {
            return dataIntActive[index];
        }
        
        public float _GetFloatData(int index)
        {
            return dataFloatActive[index];
        }
        
        public string _GetStringData(int index)
        {
            return dataStringActive[index];
        }
        
        public ParticleSystem _GetParticleSystem(int index)
        {
            return particlesSystems[index];
        }

        #region Functions
        
        private void RunFunctions(int dataPoint, int dmxOffset)
        {
            // We just read normal dmx value, do not try to read neighbours!!!!
            var dataPointIndex = dataPoint;////dmxOffset;
            var rawData = dmxRawData[dataPointIndex];
            //Debug.Log($"{dataPoint}.{dmxOffset}: {rawData}");
            
            if (smoothingAmount[dataPointIndex] > 0.0f)
            {
                rawData = (int)Mathf.Lerp(
                    dmxRawData[dataPointIndex], 
                    (int)Mathf.SmoothDamp(previousdmxRawData[dataPointIndex], 
                        dmxRawData[dataPointIndex], 
                        ref dmxRawDataVelocity[dataPointIndex], 
                        reader.updateRate * smoothingSpeed,
                        10000.0f, reader.updateRate), 
                    smoothingAmount[dataPointIndex]);
            }
            
            switch(readTypes[dataPointIndex])
            {
                case ReadType.Animation:
                    ReadAnimation(dataPointIndex, rawData);
                    break;
                case ReadType.Particles:
                    ReadParticles(dataPointIndex, rawData);
                    break;
                case ReadType.Event:
                    ReadEvent(dataPointIndex, rawData);
                    break;
                case ReadType.ObjectToggle:
                    ReadObjectToggle(dataPointIndex, rawData);
                    break;
                case ReadType.Data:
                    ReadData(dataPointIndex, rawData);
                    break;
                /*case ReadType.Transform:
                    ReadTransform(dataPoint, dmxOffset, rawData);
                    break;*/
            }
            
            previousdmxRawData[dataPointIndex] = rawData;
            dmxRawData[dataPointIndex] = rawData;
        }

        private void ReadAnimation(int i, float rawData)
        {
            if (animators[i] == null || animationParamNames[i] == "")
            {
                return;
            }
            
            switch (parameterTypes[i])
            {
                case AnimationParamType.Bool:
                    animationBoolParams[i] = rawData >= animationBoolParamsThreshold[i];
                    dataBoolActive[i] = animationBoolParams[i];

                    if(dataBoolActive[i] != dataBoolsPrevious[i])
                    {
                        animators[i].SetBool(animationParamNames[i], animationBoolParams[i]);
                        dataBoolsPrevious[i] = dataBoolActive[i];
                        if(dataUseEvent[i])
                        {
                            targetFunctions[i].SendCustomEvent(eventNames[i]);
                        }
                    }
                    break;
                case AnimationParamType.Int:
                    if(animationRangeToggle[i] == ValueOutputMethod.Value)
                    {
                        Debug.Log("Updated Int at Index With Value: " + i);
                        if(rawData >= animationIntParamsThreshold[i])
                        {
                            if(animators[i].GetInteger(animationParamNames[i]) != animationIntParams[i])
                            {
                                animators[i].SetInteger(animationParamNames[i], animationIntParams[i]);
                                dataIntActive[i] = animationIntParams[i];
                                if(dataUseEvent[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                }
                            }
                        }
                        else
                        {  
                            if(animators[i].GetInteger(animationParamNames[i]) != 0)
                            {
                                animators[i].SetInteger(animationParamNames[i], 0);
                                dataIntActive[i] = 0;
                                if(dataUseEvent[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                }
                            }
                        }
                    }
                    else if(animationRangeToggle[i] == ValueOutputMethod.Range){
                        float valf = FloatRemap(rawData, 0.0f, 255.0f, animationIntParamsRangeX[i], animationIntParamsRangeY[i]);
                        int val = Mathf.RoundToInt(valf);
                        //Debug.Log("Updated Intf With Range: " + rawdata);
                        //Debug.Log("Updated Int at Index With Range: " + valf);
                        if(animators[i].GetInteger(animationParamNames[i]) != val)
                        {
                            animators[i].SetInteger(animationParamNames[i], val);
                            dataIntActive[i] = val;
                            if(dataUseEvent[i])
                            {
                                targetFunctions[i].SendCustomEvent(eventNames[i]);
                            }
                        }
                    }
                    break;
                case AnimationParamType.Float:
                    if(animationRangeToggle[i] == ValueOutputMethod.Value)
                    {
                        float previousData = dataFloatActive[i];
                        if(rawData >= animationFloatParamsThreshold[i])
                        {
                            //if(animators[i].GetFloat(animationParamNames[i]) != animationFloatParams[i])
                            if(!Mathf.Approximately(animators[i].GetFloat(animationParamNames[i]), animationFloatParams[i]))
                            {
                                animators[i].SetFloat(animationParamNames[i], animationFloatParams[i]);
                                dataFloatActive[i] = animationFloatParams[i];
                                if(dataUseEvent[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                }
                            }
                        }
                        else
                        {
                            if(animators[i].GetFloat(animationParamNames[i]) != 0.0f)
                            {
                                animators[i].SetFloat(animationParamNames[i], 0.0f);
                                dataFloatActive[i] = 0.0f;
                                if(dataUseEvent[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                }
                            }
                        }
                        previousDataFloatActive[i] = previousData;
                    }
                    else if(animationRangeToggle[i] == ValueOutputMethod.Range)
                    {
                        float previousData = dataFloatActive[i];
                        float val = FloatRemap(rawData, 0.0f, 255.0f, animationFloatParamsRange[i].x, animationFloatParamsRange[i].y);
                        
                        //if(animators[i].GetFloat(animationParamNames[i]) != val)
                        if(!Mathf.Approximately(animators[i].GetFloat(animationParamNames[i]), val))
                        {
                            if (floatSmoothingToggle[i])
                            {
                                dataFloatActive[i] = Mathf.Clamp(Mathf.SmoothDamp(previousDataFloatActive[i], val, ref floatDataVelocity[i], (reader.updateRate * (smoothingSpeed*floatSmoothingMultiplier[i])),10000.0f, reader.updateRate),animationFloatParamsRange[i].x, animationFloatParamsRange[i].y);
                                animators[i].SetFloat(animationParamNames[i], dataFloatActive[i]);
                            }
                            else
                            {
                                animators[i].SetFloat(animationParamNames[i], val);
                                dataFloatActive[i] = val;
                            }
                            if (dataUseEvent[i])
                            {
                                targetFunctions[i].SendCustomEvent(eventNames[i]);
                            }
                        }
                        previousDataFloatActive[i] = previousData;
                    }
                    break;
                case AnimationParamType.Trigger:
                    if(!animationTriggerReset[i] && rawData >= animationTriggerParamsThreshold[i])
                    {
                        animators[i].SetTrigger(animationParamNames[i]);
                        animationTriggerReset[i] = true;
                        if(dataUseEvent[i])
                        {
                            targetFunctions[i].SendCustomEvent(eventNames[i]);
                        }
                    }
                    else if (animationTriggerReset[i] && rawData <= animationTriggerResetThreshold[i])
                    {
                        animators[i].ResetTrigger(animationParamNames[i]);
                        animationTriggerReset[i] = false;
                    }
                    else
                    {
                        animators[i].ResetTrigger(animationParamNames[i]);
                    }
                    break;
            }
        }
        
        private void ReadParticles(int i, float rawData)
        {
            if (particlesSystems[i] == null)
            {
                return;
            }
            
            if(particlesSystemsOneShotToggle[i])
            {
                if(!particlesSystemsOneShotReset[i] && rawData >= particlesSystemsThreshold[i])
                {
                    particlesSystems[i].Play();
                    particlesSystemsOneShotReset[i] = true;
                    if(dataUseEvent[i])
                    {
                        targetFunctions[i].SendCustomEvent(eventNames[i]);
                    }
                }
                else if (particlesSystemsOneShotReset[i] && rawData <= particlesSystemsOneShotResetThreshold[i])
                {
                    particlesSystemsOneShotReset[i] = false;
                }
                else if(particlesSystems[i].isPlaying)
                {
                    particlesSystems[i].Stop();
                }
                dataBoolActive[i] = particlesSystems[i].isPlaying;
            }
            else
            {
                // if(dataIntActive[i] != rawdata){
                    if(rawData >= particlesSystemsThreshold[i])
                    {
                        particlesSystems[i].Play();
                    }
                    else if(particlesSystems[i].isPlaying)
                    {
                        particlesSystems[i].Stop();
                    }
                    
                dataBoolActive[i] = rawData >= particlesSystemsThreshold[i];
                if(dataBoolActive[i] != dataBoolsPrevious[i])
                {
                    if(dataUseEvent[i])
                    {
                        targetFunctions[i].SendCustomEvent(eventNames[i]);
                    }
                }
                dataBoolsPrevious[i] =dataBoolActive[i];
            }
        }

        private void ReadEvent(int i, float rawData)
        {
            if (targetFunctions[i] == null || eventNames[i] == "")
            {
                return;
            }
            
            if(eventSpamToggle[i])
            {
                eventFireReset[i] = false;
                if(eventSpamDelay[i] > 0.0f)
                {
                    eventFireTimer[i] += Time.deltaTime;
                    if(eventFireTimer[i] >= eventSpamDelay[i] && rawData >= eventCallThreshold[i])
                    {
                        eventFireTimer[i] = 0.0f;
                        targetFunctions[i].SendCustomEvent(eventNames[i]);
                    }
                }
                else
                {
                    if(rawData >= eventCallThreshold[i])
                    {
                        targetFunctions[i].SendCustomEvent(eventNames[i]);
                    }
                }      
            }
            else
            {
                if(eventFireReset[i] == false && rawData >= eventCallThreshold[i])
                {
                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                    eventFireReset[i] = true;
                }
                else if (eventFireReset[i] == true && rawData <= eventFireResetThreshold[i])
                {
                    eventFireReset[i] = false;
                }
            }
            
        }

        private void ReadObjectToggle(int i, float rawData)
        {
            if(toggleGameobjects[i] == null){return;}
            if(invertToggle[i])
            {
                if(rawData >= eventCallThreshold[i] && toggleGameobjects[i].activeSelf == true)
                {
                    toggleGameobjects[i].SetActive(false);
                }
                else if(rawData < eventCallThreshold[i] && toggleGameobjects[i].activeSelf == false)
                {
                    toggleGameobjects[i].SetActive(true);
                }
            }
            else
            {
                if(rawData >= eventCallThreshold[i] && toggleGameobjects[i].activeSelf == false)
                {
                    toggleGameobjects[i].SetActive(true);
                }
                else if(rawData < eventCallThreshold[i] && toggleGameobjects[i].activeSelf == true)
                {
                    toggleGameobjects[i].SetActive(false);
                }
            }
            dataBoolActive[i] = toggleGameobjects[i].activeSelf;
            if(dataBoolActive[i] != dataBoolsPrevious[i])
            {
                if(dataUseEvent[i] == true){
                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                }
            }
            dataBoolsPrevious[i] = dataBoolActive[i];
        }

        private void ReadData(int i, float rawData)
        {
            if (dataUseEvent[i] && targetFunctions[i] == null)
            {
                return;
            }
            
            switch(dataTypes[i])
            {
                case DataOutputType.Bool:
                    dataBools[i] = rawData >= dataBoolsThreshold[i];
                    if (dataBools[i] != dataBoolsPrevious[i])
                    {
                        dataBoolsPrevious[i] = dataBools[i];
                        dataBoolActive[i] = dataBools[i];
                        if(dataUseEvent[i])
                        {
                            targetFunctions[i].SendCustomEvent(eventNames[i]);
                        }
                    }
                    break;
                case DataOutputType.Int:
                    if(animationRangeToggle[i] == ValueOutputMethod.Value)
                    {
                        // if(dataUseEvent[i] == true)
                        // {
                            if(rawData >= dataIntThreshold[i])
                            {     
                                if(!eventFireReset[i] && dataUseEvent[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                    eventFireReset[i] = true;
                                }
                                //Debug.Log("Activating Value!");
                                dataIntActive[i] = dataInts[i];
                            }
                            else if(rawData <= dataIntThreshold[i])
                            {
                                if (eventFireReset[i] && dataUseEvent[i])
                                {
                                    eventFireReset[i] = false;
                                }
                                dataIntActive[i] = 0;
                                //Debug.Log("Deactivating Value!");
                            }
                        // }
                    }
                    else
                    {
                        int val = IntRemap((int)rawData,0,255,dataIntRangeX[i],dataIntRangeY[i]);
                        //Debug.Log("Current Value: " + val.ToString());
                        if(val != dataIntsPrevious[i])
                        {
                            dataInts[i] = val;
                            dataIntsPrevious[i] = val;
                            dataIntActive[i] = val;
                            //Debug.Log(dataIntActive[i]);
                            if(dataUseEvent[i])
                            {
                                var delta = Mathf.Abs(rawData - previousdmxRawData[i]);
                                if(delta > dataInterpolationTolerance[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                }

                            }
                        }
                    }
                    break;
                case DataOutputType.Float:
                    if(animationRangeToggle[i] == ValueOutputMethod.Value)
                    {
                        float previousData = dataFloatActive[i];
                        
                        // if(dataUseEvent[i] == true)
                        // {
                            if (rawData >= dataFloatThreshold[i])
                            {     
                                if (!eventFireReset[i] && dataUseEvent[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                    eventFireReset[i] = true;
                                }
                                dataFloatActive[i] = dataFloats[i];
                            }
                            else if(rawData <= dataFloatThreshold[i])
                            {
                                if (eventFireReset[i] && dataUseEvent[i])
                                {
                                    eventFireReset[i] = false;
                                }
                                dataFloatActive[i] = 0.0f;
                            }
                        // }
                        previousDataFloatActive[i] = previousData;
                    }
                    else
                    {
                        float previousData = dataFloatActive[i];
                        float val = FloatRemap(rawData, 0.0f, 255.0f, dataFloatRange[i].x, dataFloatRange[i].y);
                        //if (val != dataFloatsPrevious[i])
                        if (!Mathf.Approximately(val, dataFloatsPrevious[i]))
                        {
                            if (floatSmoothingToggle[i])
                            {
                                dataFloats[i] = Mathf.Clamp(Mathf.SmoothDamp(previousDataFloatActive[i], val, ref floatDataVelocity[i], (reader.updateRate * (smoothingSpeed*floatSmoothingMultiplier[i])),10000.0f, reader.updateRate),dataFloatRange[i].x, dataFloatRange[i].y);
                                dataFloatsPrevious[i] = dataFloats[i];
                                dataFloatActive[i] = dataFloats[i]; 
                            }
                            else
                            {
                                dataFloats[i] = val;
                                dataFloatsPrevious[i] = val;
                                dataFloatActive[i] = val; 
                            }
                            if (dataUseEvent[i])
                            {
                                var delta = Mathf.Abs(rawData - previousdmxRawData[i]);
                                if(delta > dataInterpolationTolerance[i])
                                {
                                    targetFunctions[i].SendCustomEvent(eventNames[i]);
                                }
                            }
                        }
                        previousDataFloatActive[i] = previousData;
                    }
                    break;
                case DataOutputType.String:
                    int valIndex = IntRemap((int)rawData,0,255,dataStringRangeX[i],dataStringRangeY[i]);
                    string newString = dataStringList[i].stringList[valIndex];
                    if(dataUseEvent[i] && dataStringActive[i] != newString)
                    {
                        dataStringActive[i] = newString;
                        if(dataInterpolationTolerance[i] == 0)
                        {
                            targetFunctions[i].SendCustomEvent(eventNames[i]);
                            //Debug.Log("Current String: " + dataStringActive[i]);
                        }
                        else
                        {
                            var delta = Mathf.Abs(rawData - previousdmxRawData[i]);
                            if(delta > dataInterpolationTolerance[i])
                            {
                                targetFunctions[i].SendCustomEvent(eventNames[i]);
                            }
                        }
                    }
                    else
                    {
                        dataStringActive[i] = newString;
                    }
                    break;
            }
        }

        private void ReadTransform(int i, float rawData)
        {
            
        }

        #endregion
        
        public void _GetData()
        {
            // We just read normal dmx value, do not try to read neighbours!!!!
            Color[] pixelData = reader.output;
            
            // TODO: redo this into mathematical formula
            // TODO: redo this thing to read converted texture (128x128, left top -> left bottom -> Left top + 1 and so on.)

            // goes through all DataPoints and only writes DMX values to them
            // an idea is to have a length so that we can fill with more data in one DataPoint
            // that will let us use "fine", "coarse" channels, and integrate Vector3&Quaternion 
            // into this

            var lastIndex = 0;
            
            for (var dataPointIndex = 0; dataPointIndex < size; dataPointIndex++)
            {
                //var startIndex = 0;
                //startIndex = (reader.dmxPixelSize) + offsetUpBy4Pixels;
                //var pixelIndex = GetIndex(startIndex, i, out index, out xOffset, out yOffset);
                var pixelIndex = GetGlobalChannel(dmxUnviverses[dataPointIndex], dmxChannels[dataPointIndex]);
                //((dmxUnviverses[dataPointIndex] - 1) * 512) + dmxChannels[dataPointIndex] - 1;
                /*var length = dmxChannelLength[dataPointIndex]; // this is DMX channels used after pixel index (inclusive)
                for (int j = 0; j < length; j++)
                {
                    var dmxOffset = lastIndex;
                    dmxRawData[dmxOffset] = Mathf.RoundToInt(pixelData[pixelIndex + j].r * 255.0f);
                    RunFunctions(dataPointIndex, dmxOffset);
                    lastIndex++;
                }*/
                
                var dmxOffset = lastIndex;
                dmxRawData[dmxOffset] = Mathf.RoundToInt(pixelData[pixelIndex].r * 255.0f);
                RunFunctions(dataPointIndex, dmxOffset);
                //lastIndex++;

                //lastIndex = dataPointIndex + length; // write last index so we do not override values
                
                //dmxRawData[i] = Mathf.RoundToInt(pixelData[pixelIndex].r * 255.0f);
                // if(reader.isInterpolated) {dmxRawData[i+1] = Mathf.RoundToInt((pixelData[GetIndex(startindex, i+1, out index, out xOffset, out yOffset)].r) * 255.0f);}
                //RunFunctions(i);
            }
            
            /*for (var i = 0; i < pixelData.Length; i++)
            {
                var color = pixelData[i];
                if (color.r != 0) Debug.Log($"COLOR: {i} {color}");
            }*/
        }
        
        private int GetGlobalChannel(int universe, int channel)
        {
            return channel - 1 + ((universe - 1) * 512);
        }
    }
    
    #if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(VRSL_ReadBackFunction))]
    [CanEditMultipleObjects]
    public class VRSL_ReadBackFunction_Editor : Editor
    {
        private const string DisplayVersionText = "VRSL GPUReadback: <b><color=white>{0}</color></b>"; // 6a15ce is hard for eye to read.
        private const string PackagePath = "Packages/com.acchosen.vrsl-gpureadback/package.json";
        private Package _package = VRSLPackageUtility.ReadPackage(PackagePath);
        
        private SerializedProperty reader, animators, targetFunctions, particlesSystems, readTypes, eventNames, dmxRawData, smoothingAmount, smoothingSpeed, toggleGameobjects, invertToggle, dataInterpolationTolerance, floatSmoothingMultiplier;
        private SerializedProperty dmxChannelLength, dmxUnviverses, dmxChannels, parameterTypes, size, eventCallThreshold, animationFloatParamsRange, animationRangeToggle, animationFloatParamsThreshold, animationIntParamsThreshold;
        private SerializedProperty animationFloatParams, animationBoolParams, animationIntParams, animationParamNames, animationBoolParamsThreshold, animationIntParamsRangeX, animationIntParamsRangeY, particlesSystemsThreshold, floatSmoothingToggle;
        private SerializedProperty dataTypes, dataBools, dataBoolsThreshold, dataInts, dataIntThreshold, dataIntRangeX, dataIntRangeY, dataFloats, dataFloatThreshold, dataFloatRange, dataStringList, dataStringRangeX, dataStringRangeY, dataUseEvent;
        private SerializedProperty dataFoldout, particlesSystemsOneShotToggle, particlesSystemsOneShotReset, particlesSystemsOneShotResetThreshold, universalSmoothingStrength;
        private SerializedProperty dataStringActive, dataBoolActive, dataIntActive, dataFloatActive, eventSpamToggle, eventSpamDelay, eventFireReset, eventFireResetThreshold, animationTriggerReset, animationTriggerResetThreshold, animationTriggerParamsThreshold;
        
        // TODO: for some reason dmxRawValue is not valid (size: 0)
        
        private void OnEnable()
        {
            universalSmoothingStrength = serializedObject.FindProperty(nameof(universalSmoothingStrength));
            dataInterpolationTolerance = serializedObject.FindProperty(nameof(dataInterpolationTolerance));
            invertToggle = serializedObject.FindProperty(nameof(invertToggle));  
            toggleGameobjects = serializedObject.FindProperty(nameof(toggleGameobjects)); 
            smoothingSpeed = serializedObject.FindProperty(nameof(smoothingSpeed));
            smoothingAmount = serializedObject.FindProperty(nameof(smoothingAmount));
            reader = serializedObject.FindProperty(nameof(reader));
            animators = serializedObject.FindProperty(nameof(animators));
            targetFunctions = serializedObject.FindProperty(nameof(targetFunctions));
            particlesSystems = serializedObject.FindProperty(nameof(particlesSystems));
            readTypes = serializedObject.FindProperty(nameof(readTypes));
            eventNames = serializedObject.FindProperty(nameof(eventNames));
            
            dmxChannelLength = serializedObject.FindProperty(nameof(dmxChannelLength));
            dmxUnviverses = serializedObject.FindProperty(nameof(dmxUnviverses));
            dmxChannels = serializedObject.FindProperty(nameof(dmxChannels));
            
            animationParamNames = serializedObject.FindProperty(nameof(animationParamNames));
            animationBoolParams = serializedObject.FindProperty(nameof(animationBoolParams));
            animationIntParams = serializedObject.FindProperty(nameof(animationIntParams));
            animationFloatParams = serializedObject.FindProperty(nameof(animationFloatParams));
            parameterTypes = serializedObject.FindProperty(nameof(parameterTypes));
            size = serializedObject.FindProperty(nameof(size));
            eventCallThreshold = serializedObject.FindProperty(nameof(eventCallThreshold));
            animationFloatParamsRange = serializedObject.FindProperty(nameof(animationFloatParamsRange));
            animationBoolParamsThreshold = serializedObject.FindProperty(nameof(animationBoolParamsThreshold));
            animationRangeToggle = serializedObject.FindProperty(nameof(animationRangeToggle));
            animationIntParamsRangeX = serializedObject.FindProperty(nameof(animationIntParamsRangeX));
            animationIntParamsRangeY = serializedObject.FindProperty(nameof(animationIntParamsRangeY));
            animationFloatParamsThreshold = serializedObject.FindProperty(nameof(animationFloatParamsThreshold));
            animationIntParamsThreshold = serializedObject.FindProperty(nameof(animationIntParamsThreshold));
            particlesSystemsThreshold = serializedObject.FindProperty(nameof(particlesSystemsThreshold));
            dmxRawData = serializedObject.FindProperty(nameof(dmxRawData));

            animationTriggerReset = serializedObject.FindProperty(nameof(animationTriggerReset));
            animationTriggerResetThreshold = serializedObject.FindProperty(nameof(animationTriggerResetThreshold));
            animationTriggerParamsThreshold = serializedObject.FindProperty(nameof(animationTriggerParamsThreshold));

            particlesSystemsOneShotToggle = serializedObject.FindProperty(nameof(particlesSystemsOneShotToggle));
            particlesSystemsOneShotReset = serializedObject.FindProperty(nameof(particlesSystemsOneShotReset));
            particlesSystemsOneShotResetThreshold = serializedObject.FindProperty(nameof(particlesSystemsOneShotResetThreshold));

            dataStringActive = serializedObject.FindProperty(nameof(dataStringActive));
            dataBoolActive = serializedObject.FindProperty(nameof(dataBoolActive));
            dataIntActive = serializedObject.FindProperty(nameof(dataIntActive));
            dataFloatActive = serializedObject.FindProperty(nameof(dataFloatActive));
            eventSpamToggle = serializedObject.FindProperty(nameof(eventSpamToggle));
            eventSpamDelay = serializedObject.FindProperty(nameof(eventSpamDelay));
            eventFireReset = serializedObject.FindProperty(nameof(eventFireReset));
            eventFireResetThreshold = serializedObject.FindProperty(nameof(eventFireResetThreshold));

            dataFoldout = serializedObject.FindProperty(nameof(dataFoldout));

            dataTypes = serializedObject.FindProperty(nameof(dataTypes));
            dataBools = serializedObject.FindProperty(nameof(dataBools));
            dataBoolsThreshold = serializedObject.FindProperty(nameof(dataBoolsThreshold));
            dataInts = serializedObject.FindProperty(nameof(dataInts));
            dataIntThreshold = serializedObject.FindProperty(nameof(dataIntThreshold));
            dataIntRangeX = serializedObject.FindProperty(nameof(dataIntRangeX));
            dataIntRangeY = serializedObject.FindProperty(nameof(dataIntRangeY));
            dataFloats = serializedObject.FindProperty(nameof(dataFloats));
            dataFloatThreshold = serializedObject.FindProperty(nameof(dataFloatThreshold));
            dataFloatRange = serializedObject.FindProperty(nameof(dataFloatRange));
            dataStringList = serializedObject.FindProperty(nameof(dataStringList));
            dataStringRangeX = serializedObject.FindProperty(nameof(dataStringRangeX));
            dataStringRangeY = serializedObject.FindProperty(nameof(dataStringRangeY));
            dataUseEvent = serializedObject.FindProperty(nameof(dataUseEvent));
            
            floatSmoothingToggle = serializedObject.FindProperty(nameof(floatSmoothingToggle));
            floatSmoothingMultiplier = serializedObject.FindProperty(nameof(floatSmoothingMultiplier));
        }
        
        private bool EnsureCapacity(SerializedProperty property, int capacity)
        {
            if (property.arraySize < capacity)
            {
                property.arraySize = capacity;
                return true;
            }

            return false;
        }

        public void DrawElement(int index, VRSL_ReadBackFunction targetScript)
        {
            //EditorGUILayout.LabelField("Data Point #" + index, Title());
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            dmxChannelLength.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Channel Length",dmxChannelLength.GetArrayElementAtIndex(index).intValue, 1, 3);
            dmxUnviverses.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Universe",dmxUnviverses.GetArrayElementAtIndex(index).intValue, 1, 9);
            dmxChannels.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Channel",dmxChannels.GetArrayElementAtIndex(index).intValue, 1, 512);
            
            EditorGUILayout.Space(5);
            smoothingAmount.GetArrayElementAtIndex(index).floatValue = EditorGUILayout.Slider("Smoothing Strength", smoothingAmount.GetArrayElementAtIndex(index).floatValue, 0.0f, 1.0f);

            if (EnsureCapacity(dmxRawData, 512))
            {
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUILayout.LabelField("RAW VALUE:", dmxRawData.GetArrayElementAtIndex(index).floatValue.ToString(), VRSLStyles.RawDataStyle(dmxRawData.GetArrayElementAtIndex(index).floatValue));

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(readTypes.GetArrayElementAtIndex(index),VRSLStyles.Label("Type"));
            EditorGUILayout.BeginVertical("box");
            switch(readTypes.GetArrayElementAtIndex(index).enumValueIndex)
            {
                case (int)ReadType.Animation:
                    EditorGUILayout.PropertyField(animators.GetArrayElementAtIndex(index),VRSLStyles.Label("Animator"));
                    EditorGUILayout.PropertyField(animationParamNames.GetArrayElementAtIndex(index),VRSLStyles.Label("Parameter Name"));
                    EditorGUILayout.PropertyField(parameterTypes.GetArrayElementAtIndex(index),VRSLStyles.Label("Parameter Type"));
                    EditorGUILayout.BeginVertical("box");
                    switch(parameterTypes.GetArrayElementAtIndex(index).enumValueIndex)
                    {
                        case (int) AnimationParamType.Bool:
                            EditorGUILayout.PropertyField(animationBoolParams.GetArrayElementAtIndex(index),VRSLStyles.Label("Bool Value"));
                            animationBoolParamsThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",animationBoolParamsThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            EditorGUILayout.LabelField("Value:", dataBoolActive.GetArrayElementAtIndex(index).boolValue.ToString(), VRSLStyles.BoolDataStyle(dataBoolActive.GetArrayElementAtIndex(index).boolValue)); 
                            break;
                        case (int) AnimationParamType.Int:
                            EditorGUILayout.PropertyField(animationRangeToggle.GetArrayElementAtIndex(index),VRSLStyles.Label("Value Method"));
                            EditorGUILayout.BeginVertical("box");
                            if(animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Value)
                            {
                                EditorGUILayout.PropertyField(animationIntParams.GetArrayElementAtIndex(index),VRSLStyles.Label("Integer Value"));
                                animationIntParamsThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",animationIntParamsThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            }
                            else
                            {
                                //EditorGUILayout.PropertyField(animationIntParamsRange.GetArrayElementAtIndex(index), Label("Integer Range"));
                                
                                EditorGUILayout.BeginHorizontal();
                                float lw = EditorGUIUtility.labelWidth;
                                EditorGUIUtility.labelWidth = 50.0f;
                                EditorGUILayout.PropertyField(animationIntParamsRangeX.GetArrayElementAtIndex(index), VRSLStyles.Label("From:"), GUILayout.MaxWidth(100.0f));
                                //animationIntParamsRangeX.GetArrayElementAtIndex(index).intValue = Mathf.Abs(animationIntParamsRangeX.GetArrayElementAtIndex(index).intValue);
                                //GUILayout.FlexibleSpace();
                                GUILayout.Space(100.0f);
                                EditorGUILayout.PropertyField(animationIntParamsRangeY.GetArrayElementAtIndex(index), VRSLStyles.Label("To:"), GUILayout.MaxWidth(100.0f));
                                //animationIntParamsRangeY.GetArrayElementAtIndex(index).intValue = Mathf.Abs(animationIntParamsRangeY.GetArrayElementAtIndex(index).intValue);
                                EditorGUIUtility.labelWidth = lw;
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUILayout.LabelField("Value:", dataIntActive.GetArrayElementAtIndex(index).intValue.ToString(), VRSLStyles.IntDataStyle(dataIntActive.GetArrayElementAtIndex(index).intValue)); 
                            EditorGUILayout.EndVertical();
                            break;
                        case (int) AnimationParamType.Float:
                            EditorGUILayout.PropertyField(animationRangeToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Value Method"));
                            EditorGUILayout.BeginVertical("box");
                            if (animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Value)
                            {
                                EditorGUILayout.PropertyField(animationFloatParams.GetArrayElementAtIndex(index), VRSLStyles.Label("Float Value"));
                                animationFloatParamsThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",animationFloatParamsThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            }
                            else
                            {
                                EditorGUILayout.PropertyField(animationFloatParamsRange.GetArrayElementAtIndex(index), VRSLStyles.Label("Float Range"));
                                EditorGUILayout.PropertyField(floatSmoothingToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Apply Smoothing To Float Value"));
                                if (floatSmoothingToggle.GetArrayElementAtIndex(index).boolValue)
                                {
                                    EditorGUI.indentLevel++;
                                    floatSmoothingMultiplier.GetArrayElementAtIndex(index).floatValue = EditorGUILayout.Slider("Smoothing Strength Multiplier", floatSmoothingMultiplier.GetArrayElementAtIndex(index).floatValue, 1.0f, 25.0f);
                                    EditorGUI.indentLevel--;
                                }
                            } 
                            EditorGUILayout.LabelField("Value:", dataFloatActive.GetArrayElementAtIndex(index).floatValue.ToString(), VRSLStyles.FloatDataStyle(dataFloatActive.GetArrayElementAtIndex(index).floatValue)); 
                            EditorGUILayout.EndVertical();  
                            break;
                        case (int) AnimationParamType.Trigger:
                            animationTriggerParamsThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",animationTriggerParamsThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            EditorGUILayout.LabelField("READY TO FIRE: ", (!animationTriggerReset.GetArrayElementAtIndex(index).boolValue).ToString(), VRSLStyles.BoolDataStyle(!(animationTriggerReset.GetArrayElementAtIndex(index).boolValue)));
                            animationTriggerResetThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Reset Threshold",animationTriggerResetThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            break;
                    }
                    
                    EditorGUILayout.PropertyField(dataUseEvent.GetArrayElementAtIndex(index), VRSLStyles.Label("Send Event On Value Change?"));
                    if (dataUseEvent.GetArrayElementAtIndex(index).boolValue)
                    {
                        EditorGUILayout.PropertyField(targetFunctions.GetArrayElementAtIndex(index), VRSLStyles.Label("Target Function"));
                        EditorGUILayout.PropertyField(eventNames.GetArrayElementAtIndex(index), VRSLStyles.Label("Event Name"));
                        //if(animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Range){
                            dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Smoothing Tolerance",dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue, 0, 50);
                        //}
                        //EditorGUILayout.LabelField("READY TO FIRE: ", (!eventFireReset.GetArrayElementAtIndex(index).boolValue).ToString(), BoolDataStyle(!(eventFireReset.GetArrayElementAtIndex(index).boolValue)));
                        //EditorGUILayout.PropertyField(eventFireResetThreshold.GetArrayElementAtIndex(index),Label("Reset Event At:"));
                    }
                    EditorGUILayout.EndVertical();
                    break;
                case (int)ReadType.Event:
                    EditorGUILayout.PropertyField(targetFunctions.GetArrayElementAtIndex(index), VRSLStyles.Label("Target Function"));
                    EditorGUILayout.PropertyField(eventNames.GetArrayElementAtIndex(index), VRSLStyles.Label("Event Name"));
                    eventCallThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Event Activate Threshold",eventCallThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                    
                    EditorGUILayout.PropertyField(eventSpamToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Spam? (Repeatedly Fire Event When Active)"));
                    EditorGUILayout.BeginVertical("box");
                    if (eventSpamToggle.GetArrayElementAtIndex(index).boolValue)
                    {
                        EditorGUILayout.PropertyField(eventSpamDelay.GetArrayElementAtIndex(index), VRSLStyles.Label("# Of Seconds Between Each Fire"));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("READY TO FIRE: ", (!eventFireReset.GetArrayElementAtIndex(index).boolValue).ToString(), VRSLStyles.BoolDataStyle(!(eventFireReset.GetArrayElementAtIndex(index).boolValue)));
                        eventFireResetThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Event Reset Threshold",eventFireResetThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                    }
                    EditorGUILayout.EndVertical();
                    break;

                case (int)ReadType.ObjectToggle:
                    EditorGUILayout.PropertyField(toggleGameobjects.GetArrayElementAtIndex(index), VRSLStyles.Label("Target Object"));
                    eventCallThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Event Activate Threshold",eventCallThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                    EditorGUILayout.PropertyField(invertToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Invert?"));
                    try
                    {
                        GameObject targetObject = (GameObject)toggleGameobjects.GetArrayElementAtIndex(index).objectReferenceValue;
                        EditorGUILayout.LabelField("Value:", targetObject.activeSelf.ToString(),
                            VRSLStyles.BoolDataStyle(targetObject.activeSelf));
                    }
                    catch (System.Exception e)
                    {
                        e.GetType();
                    }
                    //EditorGUILayout.EndVertical();
                    EditorGUILayout.PropertyField(dataUseEvent.GetArrayElementAtIndex(index), VRSLStyles.Label("Send Event On Value Change?"));
                    if (dataUseEvent.GetArrayElementAtIndex(index).boolValue)
                    {
                        EditorGUILayout.PropertyField(targetFunctions.GetArrayElementAtIndex(index), VRSLStyles.Label("Target Function"));
                        EditorGUILayout.PropertyField(eventNames.GetArrayElementAtIndex(index), VRSLStyles.Label("Event Name"));
                        //if(animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Range){
                            dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Smoothing Tolerance",dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue, 0, 50);
                        //}
                        //EditorGUILayout.LabelField("READY TO FIRE: ", (!eventFireReset.GetArrayElementAtIndex(index).boolValue).ToString(), BoolDataStyle(!(eventFireReset.GetArrayElementAtIndex(index).boolValue)));
                        //EditorGUILayout.PropertyField(eventFireResetThreshold.GetArrayElementAtIndex(index),Label("Reset Event At:"));
                    }
                    break;
                
                case (int)ReadType.Particles:
                    EditorGUILayout.PropertyField(particlesSystems.GetArrayElementAtIndex(index), VRSLStyles.Label("Particle System"));
                    particlesSystemsThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",particlesSystemsThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                    try
                    {
                        ParticleSystem particles = targetScript._GetParticleSystem(index);
                        EditorGUILayout.LabelField("Is Playing: ", particles.isPlaying.ToString(),
                            VRSLStyles.BoolDataStyle(particles.isPlaying));
                    }
                    catch (System.Exception e)
                    {
                        e.GetType();
                    }
                    EditorGUILayout.PropertyField(particlesSystemsOneShotToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("One Shot?"));
                    if(particlesSystemsOneShotToggle.GetArrayElementAtIndex(index).boolValue)
                    {
                        EditorGUILayout.BeginVertical("box");
                        EditorGUILayout.LabelField("READY TO FIRE: ", (!particlesSystemsOneShotReset.GetArrayElementAtIndex(index).boolValue).ToString(), VRSLStyles.BoolDataStyle(!(particlesSystemsOneShotReset.GetArrayElementAtIndex(index).boolValue)));
                        particlesSystemsOneShotResetThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Reset Threshold",particlesSystemsOneShotResetThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.PropertyField(dataUseEvent.GetArrayElementAtIndex(index), VRSLStyles.Label("Send Event On Value Change?"));
                    if (dataUseEvent.GetArrayElementAtIndex(index).boolValue)
                    {
                        EditorGUILayout.PropertyField(targetFunctions.GetArrayElementAtIndex(index), VRSLStyles.Label("Target Function"));
                        EditorGUILayout.PropertyField(eventNames.GetArrayElementAtIndex(index), VRSLStyles.Label("Event Name"));
                        //if(animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Range){
                            dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Smoothing Tolerance",dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue, 0, 50);
                        //}
                        //EditorGUILayout.LabelField("READY TO FIRE: ", (!eventFireReset.GetArrayElementAtIndex(index).boolValue).ToString(), BoolDataStyle(!(eventFireReset.GetArrayElementAtIndex(index).boolValue)));
                        //EditorGUILayout.PropertyField(eventFireResetThreshold.GetArrayElementAtIndex(index),Label("Reset Event At:"));
                    }
                    break;
                case (int)ReadType.Data:
                    EditorGUILayout.PropertyField(dataTypes.GetArrayElementAtIndex(index), VRSLStyles.Label("Data Type"));
                    EditorGUILayout.BeginVertical("box");
                    switch(dataTypes.GetArrayElementAtIndex(index).enumValueIndex)
                    {
                        case (int) DataOutputType.Bool:
                            EditorGUILayout.PropertyField(dataBools.GetArrayElementAtIndex(index), VRSLStyles.Label("Bool Value"));
                            dataBoolsThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",dataBoolsThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            EditorGUILayout.LabelField("Value:", dataBoolActive.GetArrayElementAtIndex(index).boolValue.ToString(), VRSLStyles.BoolDataStyle(dataBoolActive.GetArrayElementAtIndex(index).boolValue));
                            break;
                        case (int) DataOutputType.Int:
                            EditorGUILayout.PropertyField(animationRangeToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Value Method"));
                            
                            if (animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Value)
                            {
                                EditorGUILayout.PropertyField(dataInts.GetArrayElementAtIndex(index), VRSLStyles.Label("Integer Value"));
                                dataIntThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",dataIntThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            }
                            else
                            {
                                EditorGUILayout.BeginHorizontal();
                                float lw = EditorGUIUtility.labelWidth;
                                EditorGUIUtility.labelWidth = 50.0f;
                                EditorGUILayout.PropertyField(dataIntRangeX.GetArrayElementAtIndex(index), VRSLStyles.Label("From:"), GUILayout.MaxWidth(100.0f));
                                //dataIntRangeX.GetArrayElementAtIndex(index).intValue = Mathf.Abs(dataIntRangeX.GetArrayElementAtIndex(index).intValue);
                                GUILayout.Space(100.0f);
                                EditorGUILayout.PropertyField(dataIntRangeY.GetArrayElementAtIndex(index), VRSLStyles.Label("To:"), GUILayout.MaxWidth(100.0f));
                                //dataIntRangeY.GetArrayElementAtIndex(index).intValue = Mathf.Abs(dataIntRangeY.GetArrayElementAtIndex(index).intValue);
                                EditorGUIUtility.labelWidth = lw;
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUILayout.LabelField("Value:", dataIntActive.GetArrayElementAtIndex(index).intValue.ToString(), VRSLStyles.IntDataStyle(dataIntActive.GetArrayElementAtIndex(index).intValue)); 
                            break;    
                        case (int) DataOutputType.Float:
                            EditorGUILayout.PropertyField(animationRangeToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Value Method"));
                            if (animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Value)
                            {
                                EditorGUILayout.PropertyField(dataFloats.GetArrayElementAtIndex(index), VRSLStyles.Label("Float Value"));
                                dataFloatThreshold.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Activation Threshold",dataFloatThreshold.GetArrayElementAtIndex(index).intValue, 0, 255);
                            }
                            else
                            {
                                EditorGUILayout.PropertyField(dataFloatRange.GetArrayElementAtIndex(index), VRSLStyles.Label("Float Range"));
                                EditorGUILayout.PropertyField(floatSmoothingToggle.GetArrayElementAtIndex(index), VRSLStyles.Label("Apply Smoothing To Float Value"));
                                if (floatSmoothingToggle.GetArrayElementAtIndex(index).boolValue)
                                {
                                    EditorGUI.indentLevel++;
                                    floatSmoothingMultiplier.GetArrayElementAtIndex(index).floatValue = EditorGUILayout.Slider("Smoothing Strength Multiplier", floatSmoothingMultiplier.GetArrayElementAtIndex(index).floatValue, 1.0f, 25.0f);
                                    EditorGUI.indentLevel--;
                                }
                            }
                            EditorGUILayout.LabelField("Value:", dataFloatActive.GetArrayElementAtIndex(index).floatValue.ToString(), VRSLStyles.FloatDataStyle(dataFloatActive.GetArrayElementAtIndex(index).floatValue));
                            break; 
                        case (int) DataOutputType.String:
                            targetScript.dataStringList[index] = EditorGUILayout.ObjectField("String List",dataStringList.GetArrayElementAtIndex(index).objectReferenceValue,typeof(VRSL_ReadBackFunction_StringList) ,true) as VRSL_ReadBackFunction_StringList;
                            try
                            {
                                var editor = Editor.CreateEditor(targetScript.dataStringList[index]);
                                var root = editor.DrawDefaultInspector();
                                if (root)
                                {
                                    editor.OnInspectorGUI();
                                }

                                EditorGUILayout.Space();
                                EditorGUILayout.BeginHorizontal();
                                float lw = EditorGUIUtility.labelWidth;
                                EditorGUIUtility.labelWidth = 100.0f;
                                EditorGUILayout.PropertyField(dataStringRangeX.GetArrayElementAtIndex(index),
                                    VRSLStyles.Label("Index From:"), GUILayout.MaxWidth(150.0f));
                                dataStringRangeX.GetArrayElementAtIndex(index).intValue = Mathf.Clamp(
                                    dataStringRangeX.GetArrayElementAtIndex(index).intValue, 0,
                                    targetScript.dataStringList[index].stringList.Length - 1);
                                GUILayout.Space(50.0f);
                                EditorGUILayout.PropertyField(dataStringRangeY.GetArrayElementAtIndex(index),
                                    VRSLStyles.Label("Index To:"), GUILayout.MaxWidth(150.0f));
                                dataStringRangeY.GetArrayElementAtIndex(index).intValue = Mathf.Clamp(
                                    dataStringRangeY.GetArrayElementAtIndex(index).intValue, 0,
                                    targetScript.dataStringList[index].stringList.Length - 1);
                                EditorGUIUtility.labelWidth = lw;
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.Space();
                                EditorGUILayout.LabelField("Value:",
                                    dataStringActive.GetArrayElementAtIndex(index).stringValue,
                                    VRSLStyles.StringDataStyle());
                            }
                            catch (System.Exception e)
                            {
                                e.GetType();
                            }
                            break;
                    }
                    EditorGUILayout.PropertyField(dataUseEvent.GetArrayElementAtIndex(index), VRSLStyles.Label("Send Event On Value Change?"));
                    if (dataUseEvent.GetArrayElementAtIndex(index).boolValue)
                    {
                        EditorGUILayout.PropertyField(targetFunctions.GetArrayElementAtIndex(index), VRSLStyles.Label("Target Function"));
                        EditorGUILayout.PropertyField(eventNames.GetArrayElementAtIndex(index), VRSLStyles.Label("Event Name"));
                        //if(animationRangeToggle.GetArrayElementAtIndex(index).enumValueIndex == (int)ValueOutputMethod.Range){
                            dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue = EditorGUILayout.IntSlider("Smoothing Tolerance",dataInterpolationTolerance.GetArrayElementAtIndex(index).intValue, 0, 50);
                        //}
                        //EditorGUILayout.LabelField("READY TO FIRE: ", (!eventFireReset.GetArrayElementAtIndex(index).boolValue).ToString(), BoolDataStyle(!(eventFireReset.GetArrayElementAtIndex(index).boolValue)));
                        //EditorGUILayout.PropertyField(eventFireResetThreshold.GetArrayElementAtIndex(index),Label("Reset Event At:"));
                    }
                    EditorGUILayout.EndVertical();
                    break;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            VRSLStyles.ShurikenHeaderCentered($"UnityEditor: {Application.unityVersion}, {DisplayVersionText.Replace("{0}", _package.version)}");
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            if (reader.objectReferenceValue == null && GUILayout.Button("Find VRSL GPUReadback Reader"))
            {
                reader.objectReferenceValue = Object.FindObjectOfType<VRSL_GPUReadBack>();
            }
            EditorGUILayout.PropertyField(reader);
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("Size", GUILayout.MaxWidth(50.0f));
            
            EditorGUI.BeginDisabledGroup(size.intValue <= 1);
            if (GUILayout.Button("-"))
            {
                Mathf.Min(Mathf.Max(size.intValue--, 1), 512);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(false);
            size.intValue = Mathf.Min(Mathf.Max(EditorGUILayout.IntField(Mathf.Min(Mathf.Max(size.intValue, 1), 512), GUILayout.MaxWidth(100.0f)),1), 512);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(size.intValue >= 512);
            if (GUILayout.Button("+"))
            {
                Mathf.Min(Mathf.Max(size.intValue++, 1), 512);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            //testChannel.intValue = EditorGUILayout.IntSlider("Test Channel",testChannel.intValue, 1, 512);
            //EditorGUILayout.PropertyField(extendedUniverseMode, Label("Extended Universe Mode"));
            EditorGUILayout.BeginHorizontal();
            universalSmoothingStrength.floatValue = EditorGUILayout.Slider("Global Smoothing Strength",universalSmoothingStrength.floatValue, 0.0f, 1.0f);
            if(GUILayout.Button("Apply To All"))
            {
                for(int i = 0; i < Mathf.Min(Mathf.Max(size.intValue, 1), 512); i++)
                {
                    smoothingAmount.GetArrayElementAtIndex(i).floatValue = universalSmoothingStrength.floatValue;
                }
            }
            EditorGUILayout.EndHorizontal();
            smoothingSpeed.floatValue = EditorGUILayout.Slider("Smoothing Speed (Lower = Faster)",smoothingSpeed.floatValue, 0.001f, 1.0f);
            EditorGUILayout.Space(15);
            VRSLStyles.GUILine(1);
            //EditorGUILayout.Space(10);
            var readbackfunctiotarget = (target as VRSL_ReadBackFunction);
            for(int i = 0; i < Mathf.Min(Mathf.Max(size.intValue, 1), 512); i++)
            {
                dataFoldout.GetArrayElementAtIndex(i).boolValue =
                    EditorGUILayout.BeginFoldoutHeaderGroup(dataFoldout.GetArrayElementAtIndex(i).boolValue,
                        "Data Point #" + i);// VRSLStyles.Title1Foldout());
                if(dataFoldout.GetArrayElementAtIndex(i).boolValue)
                {
                    DrawElement(i, readbackfunctiotarget);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                VRSLStyles.GUILine(1);
                //EditorGUILayout.Space(10);
            }
            Mathf.Min(Mathf.Max(size.intValue, 1), 512);
            if (EditorGUI.EndChangeCheck())
            {
                //Debug.Log("Found changes");
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            //DrawDefaultInspector();
        }
    }
    #endif
}