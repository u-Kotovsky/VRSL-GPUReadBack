using System.Linq;
using UnityEngine;
using UdonSharp;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
using VRSL.EditorScripts;
#endif

namespace VRSL.GPUReadBack
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VRSL_GPUReadBack : UdonSharpBehaviour
    {
        public VRSL_ReadBackFunction[] functionReaders;
        public int dmxPixelSize = 8;
        public float updateRate = 0.05f;
        public float startDelay = 1.0f;
        public Texture texture;
        public TextureFormat textureReadFormat = TextureFormat.RGBAFloat;
        private bool _initialize;

        [HideInInspector]
        public Color[] output;
        private float _currentTime;

        private void Start()
        {
            _initialize = false;
            _currentTime = 0.0f;
            output = new Color[texture.width * texture.height];
            SendCustomEventDelayedSeconds(nameof(_StartGPUReadback), startDelay);
        }

        private void Update()
        {
            if (!_initialize) return;
            
            if (_currentTime >= updateRate)
            {
                _MakeReadBackRequest();
                _currentTime = 0.0f;
            }
            else
            {
                _currentTime += Time.deltaTime;
            }
        }

        public void _MakeReadBackRequest()
        {
            VRCAsyncGPUReadback.Request(texture, 0, textureReadFormat, (IUdonEventReceiver)this);
        }

        public void _StartGPUReadback()
        {
            _initialize = true;
        }

        public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.LogError($"VRSL GPU ReadBack error on '{gameObject.name}'!");
                return;
            }

            if (functionReaders.Length <= 0) return;
            
            var wasSuccessful = request.TryGetData(output);
            if (!wasSuccessful) return;
            
            foreach(var readback in functionReaders)
            {
                readback.SendCustomEvent(nameof(readback._GetData));
            }
        }
    }
    
    #if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(VRSL_GPUReadBack))]
    [CanEditMultipleObjects]
    public class VRSL_GPUReadBack_Editor : Editor
    {
        private const string DisplayVersionText = "VRSL GPUReadback: <b><color=white>{0}</color></b>"; // 6a15ce is hard for eye to read.
        private const string PackagePath = "Packages/com.acchosen.vrsl-gpureadback/package.json";
        private Package _package = VRSLPackageUtility.ReadPackage(PackagePath);
        private SerializedProperty _functionReaders;
        
        private void OnEnable()
        {
            _functionReaders = serializedObject.FindProperty(nameof(_functionReaders));
        }
        
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            VRSLStyles.ShurikenHeaderCentered($"UnityEditor: {Application.unityVersion}, " + DisplayVersionText.Replace("{0}", _package.version));
            
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            
            if (GUILayout.Button("Link All ReadBack Function Objects"))
            {
                LinkAllReadBackFunctionObjects();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            
            DrawDefaultInspector();
        }

        private void LinkAllReadBackFunctionObjects()
        {
            try
            {
                var readBackFunctions = Object.FindObjectsOfType<VRSL_ReadBackFunction>();
                if (readBackFunctions.All(x => x == null)) return;
                _functionReaders.arraySize = readBackFunctions.Length;
                for(var i = 0; i < _functionReaders.arraySize; i++)
                {
                    if (readBackFunctions[i] == null)
                    {
                        Debug.LogWarning($"VRSL GPUReadBack Function at '{i}' is invalid");
                        continue;
                    }
                    _functionReaders.GetArrayElementAtIndex(i).objectReferenceValue = readBackFunctions[i];
                }
            }
            catch
            {
                // ignored
            }
        }
    }
    #endif
}