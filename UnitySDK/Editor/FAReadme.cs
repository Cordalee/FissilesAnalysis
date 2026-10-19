using System;
using UnityEngine;

namespace Fissiles.EditorUI
{
    /// <summary>
    /// The README shown in the Inspector (Editor/FAReadme.asset, opened by Tools > Fissiles Analytics > 使用说明).
    /// Plain data; <see cref="FAReadmeEditor"/> draws it.
    /// </summary>
    public class FAReadme : ScriptableObject
    {
        public string title;
        public Section[] sections;

        [Serializable]
        public class Section
        {
            public string heading;
            [TextArea] public string text;
            public string linkText;
            /// <summary>An http(s) address, or "menu:Tools/..." to run an editor menu item.</summary>
            public string url;
        }
    }
}
