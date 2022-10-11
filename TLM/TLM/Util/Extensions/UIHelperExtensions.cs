namespace TrafficManager.Util.Extensions {
    using ColossalFramework.UI;
    using ICities;
    using UnityEngine;

    internal static class UIHelperExtensions {
        public static UIComponent GetSelf(this UIHelperBase container) =>
            (container as UIHelper).self as UIComponent;
        public static T AddComponent<T>(this UIHelperBase container)
            where T : Component => container.GetSelf().gameObject.AddComponent<T>();

        public static T AddUIComponent<T>(this UIHelperBase container)
            where T : UIComponent => container.GetSelf().AddUIComponent<T>();
    }
}