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
using WaveVR_Log;

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
        private Dictionary<IntPtr, T> textures = new Dictionary<IntPtr, T>();
        private Dictionary<IntPtr, IntPtr> depthes = new Dictionary<IntPtr, IntPtr>();
        public int size { get; private set; }
        public T currentRt { get; private set; }
        public IntPtr currentPtr { get; private set; }
        public IntPtr currentDepthPtr { get; private set; }
        public bool isReleased { get; private set; }

        protected abstract T CreateTexture(TextureConfig cfg);
        protected abstract IntPtr GetNativePtr(T rt);
        protected abstract IntPtr GetNativeDepthBufferPtr(T rt);
        protected abstract void ReleaseTexture(T rt);

        // For editor test
        IntPtr[] keyArray;
#if UNITY_EDITOR
        int keyArrayIndex = 0;
#endif

        public TexturePool(TextureConfig cfg, int size, WVR_Eye eye) : base(eye)
        {
            using (var ee = Log.ee("WVR_TexMngr", "TexturePool+", "TexturePool-"))
            {
                isReleased = false;

#if UNITY_EDITOR
                // Editor doesn't need the texture queue.
                size = 1;
#endif

                this.isLeft = isLeft;
                this.size = size;
                for (int i = 0; i < size; i++)
                {
                    currentRt = CreateTexture(cfg);
                    currentPtr = GetNativePtr(currentRt);
                    currentDepthPtr = GetNativeDepthBufferPtr(currentRt);

                    textures.Add(currentPtr, currentRt);
                    depthes.Add(currentPtr, currentDepthPtr);

                    Log.d("WVR_TexMngr", "Gen rt" + currentPtr + " dp" + currentDepthPtr);
                }
                keyArray = new IntPtr[textures.Count];
                textures.Keys.CopyTo(keyArray, 0);

#if UNITY_EDITOR
                if (!Application.isEditor)
#endif
                    queue = WaveVR_Utils.WVR_StoreRenderTextures(keyArray, size, isBoth || isLeft);
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
            if (textures != null)
            {
                foreach (T texture in textures.Values)
                    ReleaseTexture(texture);

                Interop.WVR_ReleaseTextureQueue(queue);
                textures.Clear();
            }
            textures = null;
        }

        private T GetRenderTextureByPtr(IntPtr ptr)
        {
            T rt = null;
            if (!textures.TryGetValue(ptr, out rt))
                Log.e("WVR_TexMngr", "Unknown RenderTexture ID" + ((int)ptr));
            return rt;
        }

        private IntPtr GetDepthByPtr(IntPtr ptr)
        {
            IntPtr depth = IntPtr.Zero;
            if (!depthes.TryGetValue(ptr, out depth))
                Log.e("WVR_TexMngr", "Unknown RenderTexture ID" + ((int)ptr));
            return depth;
        }

        public void next()
        {
            if (isReleased)
                return;
            Log.gpl.d("WVR_TexMngr", "Get texture from queue");

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
                currentPtr = (IntPtr)WaveVR_Utils.WVR_GetAvailableTextureID(queue);
            }

            currentRt = GetRenderTextureByPtr(currentPtr);
            currentDepthPtr = GetDepthByPtr(currentPtr);
            //Log.d("WVR_TexMngr", "current rt" + currentPtr + " dp" + currentDepthPtr);
        }

        public void Release()
        {
            using (var ee = Log.ee("WVR_TexMngr", "TexturePool<" + typeof(T).Name + "> Release()+", "TexturePool Release()-"))
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
                currentPtr = IntPtr.Zero;
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
            var rt = new Texture2DArray(cfg.w, cfg.h, cfg.depth, TextureFormat.ARGB32, cfg.useMipMap, false)
            {
                wrapMode = cfg.wrapMode,
                anisoLevel = cfg.anisoLevel,
            };

            return rt;
        }

        protected override IntPtr GetNativePtr(Texture2DArray rt)
        {
            return rt == null ? IntPtr.Zero : rt.GetNativeTexturePtr();
        }

        protected override IntPtr GetNativeDepthBufferPtr(Texture2DArray rt)
        {
            return IntPtr.Zero;
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
            };
            rt.Create();
            return rt;
        }

        protected override IntPtr GetNativePtr(RenderTexture rt)
        {
            return rt == null ? IntPtr.Zero : rt.GetNativeTexturePtr();
        }

        protected override IntPtr GetNativeDepthBufferPtr(RenderTexture rt)
        {
            return rt == null ? IntPtr.Zero : rt.GetNativeDepthBufferPtr();
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

        protected override IntPtr GetNativePtr(RenderTexture rt)
        {
            return rt == null ? IntPtr.Zero : rt.GetNativeTexturePtr();
        }

        protected override IntPtr GetNativeDepthBufferPtr(RenderTexture rt)
        {
            return rt == null ? IntPtr.Zero : rt.GetNativeDepthBufferPtr();
        }

        protected override void ReleaseTexture(RenderTexture rt)
        {
            if (rt != null)
                rt.Release();
        }
    }

    public class TextureManager
    {
        private int poolSize = 3;

        public bool IsSinglePass { get; private set; }
        public bool AllowAntiAliasing { get; private set; }
        public TexturePoolRenderTexture left { get; private set; }
        public TexturePoolRenderTexture right { get; private set; }
        //public TexturePool2DArray both { get; private set; }
        public TexturePoolRenderTexture2DArray both { get; private set; }

        // Must init in Awake and make sure VRCompositor initialized.
        public TextureManager(bool isSinglePass, bool allowAntiAliasing)
        {
            using (var ee = Log.ee("WVR_TexMngr", "TextureManager(singlepass=" + isSinglePass + " allowAntiAliasing=" + allowAntiAliasing + ") +", "TextureManager()-"))
            {
                left = null;
                right = null;
                both = null;
                IsSinglePass = isSinglePass;
                AllowAntiAliasing = allowAntiAliasing;
                reset();
            }
        }

        // After Release, TextureManager will be reset when first invoke Next().
        public void ReleaseTexturePools()
        {
            using (var ee = Log.ee("WVR_TexMngr", "ReleaseTexturePools"))
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

        public void reset()
        {
            using (var ee = Log.ee("WVR_TexMngr", "reset"))
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
                if (!Application.isEditor)
                    Interop.WVR_GetRenderTargetSize(ref w, ref h);
                int screenWidth = (int)(w);
                int screenHeight = (int)(h);

                int antiAliasing = AllowAntiAliasing ? QualitySettings.antiAliasing : 0;
                if (antiAliasing == 0)
                    antiAliasing = 1;

                Log.d("WVR_TexMngr", "TextureManager: screenWidth=" + screenWidth + " screenHeight=" + screenHeight + " antiAliasing=" + antiAliasing);

                var cfg = new TextureConfig();
                cfg.w = screenWidth;
                cfg.h = screenHeight;
                cfg.depth = 24;
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

        public void Next()
        {
            if (!validate())
            {
                reset();
            }
#if UNITY_EDITOR
            if (Application.isEditor)
                return;
#endif
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

        public IntPtr GetNativePtr(WVR_Eye eye)
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
            return IntPtr.Zero;
        }

        public IntPtr GetNativePtrLR(bool isLeftEye)
        {
            return isLeftEye ? left.currentPtr : right.currentPtr;
        }

        public IntPtr GetNativePtrBoth()
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
