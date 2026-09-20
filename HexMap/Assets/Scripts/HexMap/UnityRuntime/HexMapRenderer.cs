using System;
using System.Collections.Generic;
using HexMap.Core;
using HexMap.Runtime;
using RuntimeHexMap = HexMap.Runtime.HexMap;
using UnityEngine;

namespace HexMap.UnityRuntime
{
    public sealed class HexMapRenderer : IDisposable
    {
        private readonly HexMapRenderConfig m_Config;
        private Dictionary<HexCoord, HexView> m_Views = new Dictionary<HexCoord, HexView>();
        private RuntimeHexMap m_Map;
        private HexLayout m_Layout;
        private IHexRenderStrategy m_Strategy;
        private int m_Generation;
        private bool m_IsDisposed;

        public HexMapRenderer(RuntimeHexMap map, HexLayout layout, HexMapRenderConfig config)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            m_Config = config;
            m_Map = map;
            m_Layout = layout;
            BuildInitialMap();
        }

        public RuntimeHexMap Map
        {
            get { return m_Map; }
        }

        public HexLayout Layout
        {
            get { return m_Layout; }
        }

        public int Generation
        {
            get { return m_Generation; }
        }

        public bool TryGetHexView(HexCoord coordinate, out HexView view)
        {
            EnsureNotDisposed();
            return m_Views.TryGetValue(coordinate, out view);
        }

        public void Render()
        {
            EnsureNotDisposed();
            m_Strategy.Render();
        }

        public void Rebuild(RuntimeHexMap map, HexLayout layout)
        {
            EnsureNotDisposed();
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var candidateGeneration = m_Generation + 1;
            IHexRenderStrategy candidateStrategy;
            var candidateViews = BuildCandidate(map, layout, candidateGeneration, out candidateStrategy);

            try
            {
                candidateStrategy.Activate();
            }
            catch
            {
                DisposeCandidate(candidateStrategy);
                throw;
            }

            InvalidateCurrentViews();
            var previousStrategy = m_Strategy;
            if (previousStrategy != null)
            {
                previousStrategy.Dispose();
            }

            m_Map = map;
            m_Layout = layout;
            m_Strategy = candidateStrategy;
            m_Views = candidateViews;
            m_Generation = candidateGeneration;
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            InvalidateCurrentViews();
            if (m_Strategy != null)
            {
                m_Strategy.Dispose();
                m_Strategy = null;
            }

            m_Map = null;
            m_IsDisposed = true;
        }

        private void BuildInitialMap()
        {
            var initialGeneration = m_Generation + 1;
            IHexRenderStrategy candidateStrategy;
            var candidateViews = BuildCandidate(m_Map, m_Layout, initialGeneration, out candidateStrategy);

            try
            {
                candidateStrategy.Activate();
            }
            catch
            {
                DisposeCandidate(candidateStrategy);
                throw;
            }

            m_Strategy = candidateStrategy;
            m_Views = candidateViews;
            m_Generation = initialGeneration;
        }

        private Dictionary<HexCoord, HexView> BuildCandidate(
            RuntimeHexMap map,
            HexLayout layout,
            int generation,
            out IHexRenderStrategy strategy)
        {
            strategy = CreateStrategy(m_Config.Strategy);
            try
            {
                var targets = strategy.Build(map, layout, m_Config, generation);
                var views = new Dictionary<HexCoord, HexView>();

                foreach (var cell in map.Cells)
                {
                    IHexRenderTarget target;
                    if (!targets.TryGetValue(cell.Coordinate, out target))
                    {
                        throw new InvalidOperationException(
                            "The render strategy did not create a target for " + cell.Coordinate + ".");
                    }

                    var renderHandle = new HexRenderHandle(target, generation);
                    var hexView = new HexView(cell, renderHandle);
                    hexView.SetAppearance(new HexAppearance(true, m_Config.BaseAppearanceColor, false));
                    views.Add(cell.Coordinate, hexView);
                }

                return views;
            }
            catch
            {
                DisposeCandidate(strategy);
                strategy = null;
                throw;
            }
        }

        private static IHexRenderStrategy CreateStrategy(HexMapRenderStrategy strategy)
        {
            switch (strategy)
            {
                case HexMapRenderStrategy.MeshRenderer:
                    return new MeshRendererStrategy();
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown HexMap render strategy.");
            }
        }

        private void InvalidateCurrentViews()
        {
            foreach (var view in m_Views.Values)
            {
                view.Invalidate();
            }

            m_Views.Clear();
        }

        private static void DisposeCandidate(IHexRenderStrategy strategy)
        {
            try
            {
                strategy.Dispose();
            }
            catch (Exception cleanupException)
            {
                Debug.LogException(cleanupException);
            }
        }

        private void EnsureNotDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(HexMapRenderer));
            }
        }
    }
}