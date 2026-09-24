using TMPro;
using UnityEngine;

namespace HexMap.Sample
{
    /// <summary>
    /// A minimal panel that reacts to map clicks: it opens on a plot and closes when the click hits nothing.
    /// <para>
    /// It is wired to one mode's handler, so the same plot click opens different UI depending on which mode
    /// is up. That is the whole point of the two channels, made visible in the scene.
    /// </para>
    /// <para>
    /// It subscribes in <c>OnEnable</c> and unsubscribes in <c>OnDisable</c>, which is the same rule the
    /// dispatcher uses for handlers: a hidden panel must not keep reacting. The switcher deactivates the
    /// mode's UI root, so that happens on its own when the mode changes.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapClickPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The mode handler whose PlotClicked event this panel listens to.")]
        private MonoBehaviour m_Source;

        [SerializeField]
        [Tooltip("Shown while a plot is clicked, hidden when the click hit nothing.")]
        private GameObject m_Content;

        [SerializeField]
        [Tooltip("Optional. When set, it shows the title and the clicked plot id.")]
        private TMP_Text m_Label;

        [SerializeField]
        [Tooltip("Prefixed to the plot id in the label, so the two modes' panels are told apart at a glance.")]
        private string m_Title = "Plot";

        private IMapPlotClickSource m_ResolvedSource;

        /// <summary>
        /// The plot id of the last click, or -1 while closed. Reading it is how a test or a debugger sees
        /// what the panel was told without looking at the screen.
        /// </summary>
        public int ShownPlotId { get; private set; } = -1;

        public bool IsOpen
        {
            get { return m_Content != null && m_Content.activeSelf; }
        }

        public MonoBehaviour Source
        {
            get { return m_Source; }
            set { m_Source = value; }
        }

        public GameObject Content
        {
            get { return m_Content; }
            set { m_Content = value; }
        }

        public TMP_Text Label
        {
            get { return m_Label; }
            set { m_Label = value; }
        }

        public string Title
        {
            get { return m_Title; }
            set { m_Title = value; }
        }

        private void OnEnable()
        {
            m_ResolvedSource = ResolveSource();
            if (m_ResolvedSource == null)
            {
                SetOpen(false);
                return;
            }

            m_ResolvedSource.PlotClicked += OnPlotClicked;

            // A panel that starts visible would claim a plot the player never clicked.
            SetOpen(false);
        }

        private void OnDisable()
        {
            if (m_ResolvedSource != null)
            {
                m_ResolvedSource.PlotClicked -= OnPlotClicked;
            }

            m_ResolvedSource = null;
        }

        private IMapPlotClickSource ResolveSource()
        {
            // The Unity null check comes first: an interface reference to a destroyed object is still a
            // non-null interface, so asking for the interface alone would accept a dead handler.
            if (m_Source == null)
            {
                Debug.LogError(
                    "MapClickPanel has no source handler, so it can never open. Wire the mode handler that owns this panel.",
                    this);
                return null;
            }

            var source = m_Source as IMapPlotClickSource;
            if (source == null)
            {
                Debug.LogError(
                    "MapClickPanel's source does not announce plot clicks. Wire a mode handler such as GameplayMapClickHandler or TopDownMapClickHandler.",
                    this);
            }

            return source;
        }

        private void OnPlotClicked(int plotId)
        {
            ShownPlotId = plotId;

            var hasPlot = plotId >= 0;
            if (hasPlot && m_Label != null)
            {
                m_Label.text = m_Title + " " + plotId;
            }

            SetOpen(hasPlot);
        }

        private void SetOpen(bool open)
        {
            if (m_Content != null && m_Content.activeSelf != open)
            {
                m_Content.SetActive(open);
            }
        }
    }
}
