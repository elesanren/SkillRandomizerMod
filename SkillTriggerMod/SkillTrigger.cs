using UnityEngine;

namespace SkillTriggerMod
{
    public class SkillTrigger : MonoBehaviour
    {
        private bool _triggered;
        private string _sceneName;
        private int _index;
        private string _recordKey;

        public void SetInfo(string sceneName, int index, string recordKey)
        {
            _sceneName = sceneName;
            _index = index;
            _recordKey = recordKey;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (other.GetComponent<HeroController>() == null) return;

            _triggered = true;
            if (_sceneName == "Shellwood_10")
                SkillRandomizer.GiveWallJump();
            else if (_sceneName == "Under_18")
                SkillRandomizer.GiveHarpoonDash();
            else
                SkillRandomizer.GiveRandomSkill();

            // 通过 API 记录
            SkillTriggerAPI.RecordTriggered(_recordKey);
            Destroy(gameObject);
        }
    }
}