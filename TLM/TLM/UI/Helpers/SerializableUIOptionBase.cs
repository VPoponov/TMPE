namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System.Reflection;
    using System;
    using System.Threading;
    using TrafficManager.State;
    using JetBrains.Annotations;
    using TrafficManager.Lifecycle;
    using TrafficManager.Util;
    using TrafficManager.API.Util;
    using System.Collections.Generic;

    public abstract class SerializableUIOptionBase {
        private static List<SerializableUIOptionBase> _options = new();

        public SerializableUIOptionBase() => _options.Add(this);
        public static void UpdateAll() {
            foreach (var option in _options) {
                option.OnUpdate();
            }
        }

        public abstract void OnUpdate();
    }

    public abstract class SerializableUIOptionBase<TVal, TUI, TComponent> : SerializableUIOptionBase, ILegacySerializableOption
        where TUI : UIComponent
    {

        /// <summary>Use as tooltip for readonly UI components.</summary>
        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        /* Data: */
        public delegate TVal ValidatorDelegate(TVal desired, out TVal result);

        public delegate void OnChanged(TVal value);

        public event OnChanged OnValueChanged;

        public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);

        public OnChanged Handler {
            set {
                OnValueChanged -= value;
                OnValueChanged += value;
            }
        }

        /// <summary>
        /// Optional custom validator which intercepts value changes and can inhibit event propagation.
        /// </summary>
        public ValidatorDelegate Validator { get; set; }

        [CanBeNull]
        private FieldInfo _fieldInfo;

        private string _fieldName;

        // used as internal store of value if _fieldInfo is null
        private TVal _value = default;

        public SerializableUIOptionBase(string fieldName, Scope scope)
            : base() {
            _fieldName = fieldName;
            if (scope.IsFlagSet(Scope.Savegame)) {
                _fieldInfo = typeof(SavedGameOptions).GetField(fieldName);

                if (_fieldInfo == null) {
                    throw new Exception($"SerializableUIOptionBase.ctor: `{fieldName}` does not exist");
                }
            }

            OnValueChanged = DefaultOnValueChanged;
        }

        /// <summary>type safe version of <c>Convert.ChangeType()</c>.</summary>
        private static IConvertible ChangeType(IConvertible value, Type type) => Convert.ChangeType(value, type) as IConvertible;

        /// <summary>Gets or sets the value of the field this option represents.</summary>
        public virtual TVal Value {
            get {
                if(_fieldInfo == null) {
                    return _value;
                }

                Shortcuts.AssertNotNull(SavedGameOptions.Instance, "SavedGameOptions.Instance");
                var value = _fieldInfo.GetValue(SavedGameOptions.Instance);
                if(value is IConvertible convertibleValue) {
                    return (TVal)ChangeType(convertibleValue, typeof(TVal));
                } else {
                    return (TVal)value;
                }
            }
            set {
                if (_fieldInfo == null) {
                    _value = value;
                } else if (value is IConvertible convertibleValue) {
                    IConvertible val = ChangeType(convertibleValue, _fieldInfo.FieldType);
                    Shortcuts.AssertNotNull(SavedGameOptions.Instance, "SavedGameOptions.Instance");
                    _fieldInfo.SetValue(SavedGameOptions.Instance, val);
                } else {
                    Shortcuts.AssertNotNull(SavedGameOptions.Instance, "SavedGameOptions.Instance");
                    _fieldInfo.SetValue(SavedGameOptions.Instance, value);
                }
            }
        }

        public string FieldName => _fieldInfo?.Name ?? _fieldName;

        public static implicit operator TVal(SerializableUIOptionBase<TVal, TUI, TComponent> a) => a.Value;

        public void DefaultOnValueChanged(TVal newVal) {
            if (Value.Equals(newVal)) {
                return;
            }
            Log._Debug($"SerializableUIOptionBase.DefaultOnValueChanged: `{FieldName}` changed to {newVal}");
            Value = newVal;
        }

        public abstract void Load(byte data);
        public abstract byte Save();

        public override void OnUpdate() {
            try {
                Value = Value;
            } catch (Exception ex) {
                ex.LogException();
            }
        }
        /* UI: */

        public bool HasUI => _ui != null;
        protected TUI _ui;

        private string _label;
        private string _tooltip;

        private bool _readOnly;

        private TranslatorDelegate _translator;
        public delegate string TranslatorDelegate(string key);

        public TranslatorDelegate Translator {
            get => _translator ?? Translation.Options.Get;
            set => _translator = value;
        }

        public abstract TComponent AddUI(UIHelperBase container);

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string Translate(string key) => Translator(key);

        protected abstract void UpdateTooltip();

        protected abstract void UpdateReadOnly();

        protected abstract void UpdateLabel();

        public string Label {
            get => _label ?? $"{GetType()}:{FieldName}";
            set {
                _label = value;
                UpdateLabel();
            }
        }

        public string Tooltip {
            get => _tooltip ?? string.Empty;
            set {
                _tooltip = value;
                UpdateTooltip();
            }
        }

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = value;
                UpdateReadOnly();
            }
        }

        public bool Indent { get; set; }   
    }
}