﻿using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Zenject;

namespace SpaceFox
{
    public class SceneLoadSystem
    {
        public struct SceneLoadingState
        {
            public bool IsLoaded { get; set; }
            public float Progress { get; set; }

            public static SceneLoadingState Unloaded => new(false, 0f);
            public static SceneLoadingState Loaded => new(true, 1f);

            public SceneLoadingState(bool isLoaded, float progress) : this()
            {
                IsLoaded = isLoaded;
                Progress = progress;
            }

            public static SceneLoadingState Loading(float state) => new(false, state);
        }

        private AsyncOperationHandle<SceneInstance> SceneLoadHandle;

        [Inject] private readonly ScenesList ScenesList = default;
        [Inject] private readonly UpdateProxy UpdateProxy = default;

        private readonly ObservableValue<SceneLoadingState> LoadingState = new();
        private readonly ObservableValue<AssetReference> CurrentScene = new();

        public IReadOnlyObservableValue<SceneLoadingState> State => LoadingState;
        public IReadOnlyObservableValue<AssetReference> Scene => CurrentScene;

        public void Initialize(string currentSceneName)
        {
            foreach (var scene in ScenesList.Scenes)
            {
                //TODO Check this in build
                if (scene.editorAsset.name == currentSceneName)
                {
                    CurrentScene.Value = scene;
                    LoadingState.Value = SceneLoadingState.Loaded;
                    return;
                }
            }

            LoadScene(ScenesList.MainScene);
        }

        //TODO Public method for loading scene by key

        private async void LoadScene(AssetReference scene)
        {
            LoadingState.Value = SceneLoadingState.Unloaded;
            CurrentScene.Value = scene;

            if (SceneLoadHandle.IsValid())
                Addressables.Release(SceneLoadHandle);

            SceneLoadHandle = Addressables.LoadSceneAsync(scene);
            var loadingStateUpdater = UpdateProxy.Update.Subscribe(
                () => LoadingState.Value = SceneLoadingState.Loading(SceneLoadHandle.PercentComplete));

            await SceneLoadHandle.Task;

            loadingStateUpdater.Dispose();
            LoadingState.Value = SceneLoadingState.Loaded;
        }
    }
}
