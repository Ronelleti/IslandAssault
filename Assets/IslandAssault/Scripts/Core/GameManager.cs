using UnityEngine;

namespace IslandAssault
{
    /// <summary>
    /// Creates the whole game automatically when you press Play in any scene.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            var go = new GameObject("IslandAssault");
            go.AddComponent<GameManager>();
        }
    }

    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public SaveData save;
        public BaseMode baseMode;
        public AttackMode attackMode;

        float autosaveTimer;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            // Phones: landscape only
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            CameraController.Setup();
            WorldEnvironment.Create();
            UIManager.Create();

            save = SaveSystem.Load();
            if (save == null) save = SaveData.NewGame();

            baseMode = gameObject.AddComponent<BaseMode>();
            attackMode = gameObject.AddComponent<AttackMode>();
            baseMode.enabled = false;
            attackMode.enabled = false;
        }

        void Start()
        {
            EnterHome();
        }

        void Update()
        {
            GameInput.Tick();
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer > 15f) { autosaveTimer = 0f; Save(); }
        }

        // ---------------- Modes ----------------

        public void EnterHome()
        {
            attackMode.Exit();
            attackMode.enabled = false;
            baseMode.enabled = true;
            baseMode.Enter();
        }

        public void EnterAttack()
        {
            baseMode.Exit();
            baseMode.enabled = false;
            Save();
            attackMode.enabled = true;
            attackMode.Enter();
        }

        // ---------------- Economy ----------------

        public int HQLevel
        {
            get
            {
                foreach (var b in save.buildings)
                    if (b.type == (int)BuildingType.HQ) return Mathf.Max(1, b.level);
                return 1;
            }
        }

        public int StorageCap { get { return GameData.StorageCap(HQLevel); } }

        public bool CanAfford(int gold, int wood) { return save.gold >= gold && save.wood >= wood; }

        public bool Spend(int gold, int wood)
        {
            if (!CanAfford(gold, wood)) return false;
            save.gold -= gold;
            save.wood -= wood;
            return true;
        }

        /// <summary>Adds resources up to the storage cap. Returns what actually fit.</summary>
        public float AddGold(float amount)
        {
            float before = save.gold;
            save.gold = Mathf.Min(StorageCap, save.gold + amount);
            if (save.gold < before) save.gold = before; // never reduce if already above cap
            return save.gold - before;
        }

        public float AddWood(float amount)
        {
            float before = save.wood;
            save.wood = Mathf.Min(StorageCap, save.wood + amount);
            if (save.wood < before) save.wood = before;
            return save.wood - before;
        }

        // ---------------- Saving ----------------

        public void Save()
        {
            if (baseMode != null && baseMode.enabled) baseMode.WriteToSave();
            SaveSystem.Save(save);
        }

        public void ResetGame()
        {
            baseMode.Exit();
            SaveSystem.Delete();
            save = SaveData.NewGame();
            SaveSystem.Save(save);
            baseMode.Enter();
            UIManager.I.Toast("New game started!");
        }

        void OnApplicationPause(bool paused) { if (paused && save != null) Save(); }
        void OnApplicationQuit() { if (save != null) Save(); }
    }
}
