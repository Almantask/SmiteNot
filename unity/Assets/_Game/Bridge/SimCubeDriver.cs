using GodGame.Sim;
using UnityEngine;

namespace GodGame.Bridge
{
    /// <summary>
    /// M0 smoke test: runs the placeholder simulation and moves this object with one of its units,
    /// proving the simulation package, the tick and the read-only bridge work end to end.
    /// </summary>
    public sealed class SimCubeDriver : MonoBehaviour
    {
        [SerializeField]
        private int _seed = 1;

        [SerializeField, Min(1)]
        private int _unitCount = 1;

        [SerializeField, Min(0)]
        private int _unitIndex;

        [SerializeField, Min(2)]
        private int _arenaWidthMetres = 20;

        [SerializeField, Min(2)]
        private int _arenaDepthMetres = 10;

        private SimHost _host;

        public SimHost Host => _host;

        private void OnEnable()
        {
            // Built fresh on every enable: no static state, so Enter Play Mode without domain reload is safe.
            var setup = new PlaceholderSetup(
                seed: (ulong)_seed,
                unitCount: _unitCount,
                arenaWidthMm: _arenaWidthMetres * 1000,
                arenaDepthMm: _arenaDepthMetres * 1000
            );
            _host = new SimHost(setup);
            _unitIndex = Mathf.Clamp(_unitIndex, 0, _unitCount - 1);
        }

        private void OnDisable()
        {
            _host = null;
        }

        private void Update()
        {
            _host.Advance(Time.deltaTime);
            _host.GetPositionMetres(_unitIndex, out double x, out double z);
            transform.position = new Vector3((float)x, transform.position.y, (float)z);
        }
    }
}
