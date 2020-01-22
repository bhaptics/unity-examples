// "WaveVR SDK 
// © 2017 HTC Corporation. All Rights Reserved.
//
// Unless otherwise required by copyright law and practice,
// upon the execution of HTC SDK license agreement,
// HTC grants you access to and use of the WaveVR SDK(s).
// You shall fully comply with all of HTC’s SDK license agreement terms and
// conditions signed by you and all SDK and API requirements,
// specifications, and documentation provided by HTC to You."

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using WVR_Log;
using UnityEngine.Profiling;

namespace wvr.render
{
	public struct TextureConfig
	{
		public int w;
		public int h;
		public int depth;
		public RenderTextureFormat format;
		public bool useMipMap;
		public int anisoLevel;
		public FilterMode filterMode;
		public TextureWrapMode wrapMode;
		public int antiAliasing;
	}

	public class EyeConfig
	{
		public EyeConfig(WVR_Eye eye)
		{
			if (eye == WVR_Eye.WVR_Eye_Both)
			{
				isLeft = false;
				isRight = false;
				isBoth = true;
			}
			else
			{
				isLeft = eye == WVR_Eye.WVR_Eye_Left;
				isRight = !isLeft;
			}
		}

		public bool isBoth { get; protected set; }
		public bool isLeft { get; protected set; }
		public bool isRight { get; protected set; }
	}

	public abstract class TexturePool<T> : EyeConfig where T : class
	{
		private IntPtr queue;
		private List<T> rts = new List<T>();
		private Dictionary<Int32, T> textures = new Dictionary<Int32, T>();
		private Dictionary<Int32, Int32> depthes = new Dictionary<Int32, Int32>();
		protected TextureConfig cfg;
		public TextureConfig Config { get { return cfg; } set { cfg = value; } }

		public int size { get; private set; }
		public T currentRt { get; private set; }
		public Int32 currentPtr { get; private set; }
		public Int32 currentDepthPtr { get; private set; }
		public bool isReleased { get; private set; }

		protected abstract T CreateTexture(TextureConfig cfg);
		protected abstract bool CfgValidate(T rt);  // For resize
		protected abstract Int32 GetNativePtr(T rt);
		protected abstract Int32 GetNativeDepthBufferPtr(T rt);
		protected abstract void ReleaseTexture(T rt);

		private Int32[] keyArray;

		// For editor test
#if UNITY_EDITOR
		int keyArrayIndex = 0;
#endif

		public TexturePool(TextureConfig cfg, int size, WVR_Eye eye) : base(eye)
		{
			this.cfg = cfg;
			using (var ee = Log.ee(TextureManager.TAG, "TexturePool+", "TexturePool-"))
			{
				isReleased = false;

#if UNITY_EDITOR
				// Editor doesn't need the texture queue.
				size = 1;
#endif

				this.isLeft = isLeft;
				this.size = size;

				// It will always get an error internally due to our EGL hacking.  Close the callstack dump for speed.
				var origin = Application.GetStackTraceLogType(LogType.Error);
				Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);

				for (int i = 0; i < size; i++)
				{
					rts.Add(CreateTexture(cfg));
				}

				// Call GetNativePtr once after all texture are created.  Try not to block render thread too long.
				for (int i = 0; i < size; i++)
				{
					T rt = rts[i];
					currentPtr = GetNativePtr(rt);
					currentDepthPtr = GetNativeDepthBufferPtr(rt);

					textures.Add(currentPtr, rt);
					depthes.Add(currentPtr, currentDepthPtr);

					Log.i(TextureManager.TAG, "Gen rt" + currentPtr + " dp" + currentDepthPtr);
				}

				Log.e(TextureManager.TAG, "Don't worry about the libEGL and Unity error showing above.  They are safe and will not crash your game.");
				Application.SetStackTraceLogType(LogType.Error, origin);

				keyArray = new Int32[textures.Count];
				textures.Keys.CopyTo(keyArray, 0);

#if UNITY_EDITOR
				if (!Application.isEditor)
#endif
					if (eye == WVR_Eye.WVR_Eye_Both)
						queue = WaveVR_Utils.WVR_StoreRenderTextures(keyArray, size, isBoth || isLeft, WVR_TextureTarget.WVR_TextureTarget_2D_ARRAY);
					else
						queue = WaveVR_Utils.WVR_StoreRenderTextures(keyArray, size, isBoth || isLeft, WVR_TextureTarget.WVR_TextureTarget_2D);
			}
		}

		~TexturePool()
		{
			keyArray = null;
#if UNITY_EDITOR
			keyArrayIndex = 0;

			// Application.isEditor can't be used in the destructure.
			bool editor = true;
			if (editor)
				return;
#endif
			depthes.Clear();
			depthes = null;

			rts.Clear();
			rts = null;

			if (textures != null)
			{
				foreach (T texture in textures.Values)
					ReleaseTexture(texture);

				Interop.WVR_ReleaseTextureQueue(queue);
				textures.Clear();
			}
			textures = null;
		}

		private T GetRenderTextureByPtr(Int32 ptr)
		{
			T rt = null;
			if (!textures.TryGetValue(ptr, out rt))
				Log.e(TextureManager.TAG, "Unknown RenderTexture ID" + ((int)ptr));
			return rt;
		}

		private Int32 GetDepthByPtr(Int32 ptr)
		{
			Int32 depth = 0;
			if (!depthes.TryGetValue(ptr, out depth))
				Log.e(TextureManager.TAG, "Unknown RenderTexture ID" + ((int)ptr));
			return depth;
		}

		public void next()
		{
			if (isReleased)
				return;
			Profiler.BeginSample("Next");
			Log.gpl.d(TextureManager.TAG, "Get texture from queue");

#if UNITY_EDITOR
			if (Application.isEditor)
			{
				// We can test the dictionary in editor.
				currentPtr = keyArray[keyArrayIndex++];
				if (keyArrayIndex >= textures.Count)
					keyArrayIndex = 0;
			}
			else
#endif
			{
				currentPtr = (Int32)WaveVR_Utils.WVR_GetAvailableTextureID(queue);
			}

			currentRt = GetRenderTextureByPtr(currentPtr);
			currentDepthPtr = GetDepthByPtr(currentPtr);
			//Log.d(TextureManager.TAG, "current rt" + currentPtr + " dp" + currentDepthPtr);
			if (!CfgValidate(currentRt))
				ReplaceCurrentWithNewTexture();
			Profiler.EndSample();
		}

		// Replace the texture, which is 'not used by ATW', to new configured another texture.
		// Create full textures of a queue in once is very heavy loading.  Thus, we replace 
		// the texture of a queue one by one.
		private void ReplaceCurrentWithNewTexture()
		{
			Profiler.BeginSample("NewTexture");

			// Remove old texture from lists.
			textures.Remove(currentPtr);
			depthes.Remove(currentPtr);
			rts.Remove(currentRt);
			ReleaseTexture(currentRt);

#if UNITY_EDITOR
			if (!Application.isEditor)
#endif
				if (queue != IntPtr.Zero)
				{
					Interop.WVR_ReleaseTextureQueue(queue);
				}

			// Create new texture
			T newRt;
			int newPtr, newDepthPtr;
			IntPtr newQueue = IntPtr.Zero;
			{
				// It will always get an error internally due to our EGL hacking.  Close the callstack dump for speed.
				var origin = Application.GetStackTraceLogType(LogType.Error);
				Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);

				newRt = CreateTexture(cfg);
				rts.Add(newRt);

				newPtr = GetNativePtr(newRt);
				newDepthPtr = GetNativeDepthBufferPtr(newRt);

				// The libEGL and Unity error will show because WaveVR change the egl surface for necessary.  Every game using WaveVR Unity plugin will have these logs.
				Log.e(TextureManager.TAG, "If the libEGL and Unity errors appeared above, don't panic or report a bug.  They are safe and will not crash your game.");
				Application.SetStackTraceLogType(LogType.Error, origin);

				textures.Add(newPtr, newRt);
				depthes.Add(newPtr, newDepthPtr);

				Log.i(TextureManager.TAG, Log.CSB.Append("Gen rt").Append(newPtr).Append(" dp").Append(newDepthPtr).ToString());
			}

			if (keyArray.Length != textures.Count)
				keyArray = new Int32[textures.Count];
			textures.Keys.CopyTo(keyArray, 0);

#if UNITY_EDITOR
			if (!Application.isEditor)
#endif
				newQueue = WaveVR_Utils.WVR_StoreRenderTextures(keyArray, size, isBoth || isLeft, isBoth ? WVR_TextureTarget.WVR_TextureTarget_2D_ARRAY : WVR_TextureTarget.WVR_TextureTarget_2D);

			// Assign new to curent
			currentRt = newRt;
			currentPtr = newPtr;
			currentDepthPtr = newDepthPtr;
			queue = newQueue;

			Profiler.EndSample();
		}

		public void Release()
		{
			using (var ee = Log.ee(TextureManager.TAG, "TexturePool<" + typeof(T).Name + "> Release()+", "TexturePool Release()-"))
			{
				if (isReleased)
					return;
				isReleased = true;

				keyArray = null;

				if (textures != null)
				{
					foreach (T texture in textures.Values)
						ReleaseTexture(texture);
					textures.Clear();
				}
				textures = null;

#if UNITY_EDITOR
				if (!Application.isEditor)
#endif
				{
					if (queue != IntPtr.Zero)
					{
						Interop.WVR_ReleaseTextureQueue(queue);
					}
					queue = IntPtr.Zero;
				}

				size = 0;
				currentPtr = 0;
				currentDepthPtr = 0;
				currentRt = null;
			}
		}
	}

	public class TexturePool2DArray : TexturePool<Texture2DArray>
	{
		public TexturePool2DArray(TextureConfig cfg, int size) : base(cfg, size, WVR_Eye.WVR_Eye_Both)
		{
		}

		protected override Texture2DArray CreateTexture(TextureConfig cfg)
		{
			var rt = new Texture2DArray(cfg.w, cfg.h, 2, TextureFormat.ARGB32, cfg.useMipMap, false)
			{
				wrapMode = cfg.wrapMode,
				anisoLevel = cfg.anisoLevel,
			};

			return rt;
		}

		protected override bool CfgValidate(Texture2DArray rt)
		{
			// Only compare these...
			return rt.width == cfg.w && rt.height == cfg.h;
		}

		protected override Int32 GetNativePtr(Texture2DArray rt)
		{
			return rt == null ? 0 : (Int32)rt.GetNativeTexturePtr();
		}

		protected override Int32 GetNativeDepthBufferPtr(Texture2DArray rt)
		{
			return 0;
		}

		protected override void ReleaseTexture(Texture2DArray rt)
		{
		}
	}

	public class TexturePoolRenderTexture2DArray : TexturePool<RenderTexture>
	{
		public TexturePoolRenderTexture2DArray(TextureConfig cfg, int size) : base(cfg, size, WVR_Eye.WVR_Eye_Both)
		{
		}

		protected override RenderTexture CreateTexture(TextureConfig cfg)
		{
			var rt = new RenderTexture(cfg.w, cfg.h, cfg.depth, cfg.format, RenderTextureReadWrite.Default)
			{
				useMipMap = cfg.useMipMap,
				wrapMode = cfg.wrapMode,
				filterMode = cfg.filterMode,
				anisoLevel = cfg.anisoLevel,
				antiAliasing = cfg.antiAliasing,
				dimension = TextureDimension.Tex2DArray,
				volumeDepth = 2,
				vrUsage = VRTextureUsage.TwoEyes,
			};
			rt.Create();

			return rt;
		}

		protected override bool CfgValidate(RenderTexture rt)
		{
			// Only compare these...
			return rt.width == cfg.w && rt.height == cfg.h;
		}

		protected override Int32 GetNativePtr(RenderTexture rt)
		{
			return rt == null ? 0 : (Int32)rt.GetNativeTexturePtr();
		}

		protected override Int32 GetNativeDepthBufferPtr(RenderTexture rt)
		{
			return rt == null ? 0 : (Int32)rt.GetNativeDepthBufferPtr();
		}

		protected override void ReleaseTexture(RenderTexture rt)
		{
			if (rt != null)
				rt.Release();
		}
	}

	public class TexturePoolRenderTexture : TexturePool<RenderTexture>
	{
		public TexturePoolRenderTexture(TextureConfig cfg, int size, WVR_Eye eye) : base(cfg, size, eye)
		{
		}

		protected override RenderTexture CreateTexture(TextureConfig cfg)
		{
			var rt = new RenderTexture(cfg.w, cfg.h, cfg.depth, cfg.format, RenderTextureReadWrite.Default)
			{
				useMipMap = cfg.useMipMap,
				wrapMode = cfg.wrapMode,
				filterMode = cfg.filterMode,
				anisoLevel = cfg.anisoLevel,
				antiAliasing = cfg.antiAliasing,
			};
			rt.Create();

			return rt;
		}

		protected override bool CfgValidate(RenderTexture rt)
		{
			// Only compare these...
			return rt.width == cfg.w && rt.height == cfg.h;
		}

		protected override Int32 GetNativePtr(RenderTexture rt)
		{
			return rt == null ? 0 : (Int32)rt.GetNativeTexturePtr();
		}

		protected override Int32 GetNativeDepthBufferPtr(RenderTexture rt)
		{
			return rt == null ? 0 : (Int32)rt.GetNativeDepthBufferPtr();
		}

		protected override void ReleaseTexture(RenderTexture rt)
		{
			if (rt != null)
				rt.Release();
		}
	}

	public class TextureManager
	{
		public static readonly string TAG = "WVR_TexMngr";
		private int poolSize = 3;

		public bool IsSinglePass { get; private set; }
		public bool AllowAntiAliasing { get; private set; }
		public TexturePoolRenderTexture left { get; private set; }
		public TexturePoolRenderTexture right { get; private set; }
		//public TexturePool2DArray both { get; private set; }
		public TexturePoolRenderTexture2DArray both { get; private set; }
		private int screenWidth = 1024, screenHeight = 1024;
		private readonly float pixelDensity = 1.0f;
		private float resolutionScale = 1.0f;
		public float PixelDensity { get { return pixelDensity; } }
		public float ResolutionScale { get { return resolutionScale; } }
		public float FinalScale { get { return pixelDensity * resolutionScale; } }

		// Must init in Awake and make sure VRCompositor initialized.
		public TextureManager(bool isSinglePass, bool allowAntiAliasing, float pixelDensity = 1.0f, float resolutionScale = 1.0f)
		{
			using (var ee = Log.ee(TAG, "TextureManager(singlepass=" + isSinglePass + " allowAntiAliasing=" + allowAntiAliasing + ") +", "TextureManager()-"))
			{
				left = null;
				right = null;
				both = null;

				this.pixelDensity = pixelDensity;
				this.resolutionScale = resolutionScale;

				IsSinglePass = isSinglePass;
				AllowAntiAliasing = allowAntiAliasing;
				reset();
			}
		}

		// After Release, TextureManager will be reset when first invoke Next().
		public void ReleaseTexturePools()
		{
			using (var ee = Log.ee(TAG, "ReleaseTexturePools"))
			{
				// Not set pools to null.  The pools can be accessed after released.
				if (left != null)
					left.Release();

				if (right != null)
					right.Release();

				if (both != null)
					both.Release();
			}
		}

		private float GetPreviewImageRatio()
		{
			float ratio = 1.0f;

			// For Preivew only.
#if UNITY_EDITOR
			if (WaveVR.EnableSimulator)
			{
				int connectType = UnityEditor.EditorPrefs.GetInt("ConnectType");

				if (connectType == 1)  // Wifi
				{
					bool enablePreview = UnityEditor.EditorPrefs.GetBool("EnablePreviewImage");
					if (enablePreview)
					{
						int TargetSizeRatio = UnityEditor.EditorPrefs.GetInt("TargetSizeRatio");
						if (TargetSizeRatio == 1) ratio = 1f;
						if (TargetSizeRatio == 2) ratio = 0.8f;
						if (TargetSizeRatio == 3) ratio = 0.6f;
						if (TargetSizeRatio == 4) ratio = 0.4f;
						if (TargetSizeRatio == 5) ratio = 0.2f;
					}
				}
			}
#endif
			Log.d(TAG, "Preview image ratio = " + ratio);
			return ratio;
		}

		private static int ToMultipleOfTwo(int value)
		{
			if ((value % 2) == 0)
				return value;
			return value + 1;
		}

		public void reset()
		{
			using (var ee = Log.ee(TAG, "reset"))
			{
#if UNITY_EDITOR
				poolSize = 3;
				if (!Application.isEditor)
#endif
				{
					poolSize = WaveVR_Utils.WVR_GetNumberOfTextures();
				}

				int size = Mathf.Max(Screen.width / 2, Screen.height);
				uint w = (uint)size;
				uint h = (uint)size;
				Interop.WVR_GetRenderTargetSize(ref w, ref h);
				screenWidth = (int) w;
				screenHeight = (int) h;

				float previewRatio = GetPreviewImageRatio();
				int scaledWidth = ToMultipleOfTwo((int)(screenWidth * FinalScale * previewRatio));
				int scaledHeight = ToMultipleOfTwo((int)(screenHeight * FinalScale * previewRatio));

				int antiAliasing = AllowAntiAliasing ? QualitySettings.antiAliasing : 0;
				if (antiAliasing == 0)
					antiAliasing = 1;

				Log.d(TAG, "Texture width=" + scaledWidth + " height=" + scaledHeight + " antiAliasing=" + antiAliasing);

				var cfg = new TextureConfig();
				cfg.w = scaledWidth;
				cfg.h = scaledHeight;
				cfg.depth = 24;  // Only 24 has StencilBuffer.  See Unity document.  Only 24 can let VR work normally.
				cfg.format = RenderTextureFormat.ARGB32;
				cfg.useMipMap = false;
				cfg.wrapMode = TextureWrapMode.Clamp;
				cfg.filterMode = FilterMode.Bilinear;
				cfg.anisoLevel = 1;
				cfg.antiAliasing = antiAliasing;

				if (validate())
					ReleaseTexturePools();

				if (IsSinglePass)
				{
					both = new TexturePoolRenderTexture2DArray(cfg, poolSize);
				}
				else
				{
					left = new TexturePoolRenderTexture(cfg, poolSize, WVR_Eye.WVR_Eye_Left);
					right = new TexturePoolRenderTexture(cfg, poolSize, WVR_Eye.WVR_Eye_Right);
				}
			}  // reset log.ee
		}

		public bool validate()
		{
			if (IsSinglePass)
				return both != null && !both.isReleased;
			else
				return left != null && right != null && !left.isReleased && !right.isReleased;
		}

		public void Resize(float resolutionScale = 1.0f)
		{
			resolutionScale = Mathf.Clamp(resolutionScale, 0.1f, 1.0f);

			// Too similar change will be skipped.  But always can reset to 1.0f.
			if (resolutionScale < 1.0f && Mathf.Abs(this.resolutionScale - resolutionScale) < 0.0001f)
			{
				Log.d(TAG, "Skip similar resize change.");
				return;
			}

			this.resolutionScale = resolutionScale;
			float previewRatio = GetPreviewImageRatio();
			int scaledWidth = ToMultipleOfTwo((int)(screenWidth * FinalScale * previewRatio));
			int scaledHeight = ToMultipleOfTwo((int)(screenHeight * FinalScale * previewRatio));
			Log.d(TAG, Log.CSB.Append("Resized texture width=").Append(scaledWidth).Append(", height=").Append(scaledHeight).ToString());

			if (IsSinglePass)
			{
				TextureConfig cfg = both.Config;
				cfg.w = scaledWidth;
				cfg.h = scaledHeight;

				both.Config = cfg;
			}
			else
			{
				TextureConfig cfg = left.Config;
				cfg.w = scaledWidth;
				cfg.h = scaledHeight;

				left.Config = cfg;

				cfg = right.Config;
				cfg.w = scaledWidth;
				cfg.h = scaledHeight;

				right.Config = cfg;
			}
		}

		public void Next()
		{
			if (!validate())
			{
				reset();
			}
			if (IsSinglePass)
			{
				both.next();
			}
			else
			{
				left.next();
				right.next();
			}
		}

		public Int32 GetNativePtr(WVR_Eye eye)
		{
			switch (eye)
			{
				case WVR_Eye.WVR_Eye_Both:
					return both.currentPtr;
				case WVR_Eye.WVR_Eye_Left:
					return left.currentPtr;
				case WVR_Eye.WVR_Eye_Right:
					return right.currentPtr;
			}
			return 0;
		}

		public Int32 GetNativePtrLR(bool isLeftEye)
		{
			return isLeftEye ? left.currentPtr : right.currentPtr;
		}

		public Int32 GetNativePtrBoth()
		{
			return both.currentPtr;
		}

		public Texture GetRenderTexture(WVR_Eye eye)
		{
			switch (eye)
			{
				case WVR_Eye.WVR_Eye_Both:
					return both.currentRt;
				case WVR_Eye.WVR_Eye_Left:
					return left.currentRt;
				case WVR_Eye.WVR_Eye_Right:
					return right.currentRt;
			}
			return null;
		}

		public RenderTexture GetRenderTextureBoth()
		{
			return both.currentRt;
		}

		public RenderTexture GetRenderTextureLR(bool isLeftEye)
		{
			return isLeftEye ? left.currentRt : right.currentRt;
		}
	}
}
