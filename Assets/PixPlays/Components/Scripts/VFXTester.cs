using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 1. New Input System 네임스페이스 추가

namespace PixPlays.ElementalVFX
{

    public class VFXTester : MonoBehaviour
    {
        [System.Serializable]
        public class TestingData
        {
            public string Name;
            public AnimationClip clip;
            public float VfxSpawnDelay;
            public BindingPointType Source;
            public float _Duration;
            public float _Radius;
            public BaseVfx VFX;
        }

        [SerializeField] List<TestingData> _Data;
        [SerializeField] Character _Character;
        [SerializeField] string _CurrentData;

        private int index = 0;

        /// <summary>
        /// [수정됨] Input.GetKeyDown 대신 Keyboard.current를 사용합니다.
        /// </summary>
        public void Update()
        {
            // 2. 현재 키보드 장치가 있는지 확인 (없으면 에러 방지)
            if (Keyboard.current == null)
            {
                return;
            }

            // 3. [수정] Old: Input.GetKeyDown(KeyCode.LeftArrow)
            if (Keyboard.current[Key.LeftArrow].wasPressedThisFrame)
            {
                index--;
                if (index < 0)
                {
                    index = _Data.Count - 1;
                }
                _CurrentData = _Data[index].VFX.name;
            }

            // 4. [수정] Old: Input.GetKeyDown(KeyCode.RightArrow)
            if (Keyboard.current[Key.RightArrow].wasPressedThisFrame)
            {
                index++;
                if (index >= _Data.Count)
                {
                    index = 0;
                }
                _CurrentData = _Data[index].VFX.name;
            }

            // 5. [수정] Old: Input.GetKeyDown(KeyCode.Space)
            if (Keyboard.current[Key.Space].wasPressedThisFrame)
            {
                StartCoroutine(Coroutine_Spanw());
            }
        }

        IEnumerator Coroutine_Spanw()
        {
            _Character.PlayAnimation("New Animation", _Data[index].clip);
            yield return new WaitForSeconds(_Data[index].VfxSpawnDelay);
            BaseVfx go = Instantiate(_Data[index].VFX);
            Transform sourcePoint = _Character.BindingPoints.GetBindingPoint(_Data[index].Source);
            var vfxData = new VfxData(sourcePoint, _Character.GetTarget(), _Data[index]._Duration, _Data[index]._Radius);
            vfxData.SetGround(_Character.BindingPoints.GetBindingPoint(BindingPointType.Ground));
            go.Play(vfxData); // [오타 수정] vfsData -> vfxData
        }
    }
}