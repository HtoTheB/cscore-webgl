﻿using com.csutil.datastructures;
using com.csutil.model.immutable;
using com.csutil.ui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace com.csutil {

    public static class UiExtensions {

        public static ViewStack GetViewStack(this GameObject gameObject) {
            var vs = gameObject.GetComponentInParents<ViewStack>();
            if (vs == null) { Log.e("Not part of a UI managed by a ViewStack", gameObject); }
            return vs;
        }

        /// <summary> Returns a list of tasks that represents the numbers of clicks the user did on the button </summary>
        public static IList<Task> SetOnClickActionAsync(this Button self, Func<GameObject, Task> onClickAction) {
            var clicks = new List<Task>();
            TaskCompletionSource<bool> tcs = null; // Set to null to skip the first .SetFromTask() below
            var firstClickTask = self.SetOnClickAction(async go => {
                var t = onClickAction(go);
                tcs?.SetFromTask(t);
                tcs = new TaskCompletionSource<bool>();
                clicks.Add(tcs.Task); // Always add the next not yet used task to the list so that the last Task is always pending
                await t;
            });
            clicks.Add(firstClickTask);
            return clicks;
        }

        public static Task SetOnClickAction(this Button self, Action<GameObject> onClickAction) {
            SetupOnClickEventObject(self);
            return self.AddOnClickAction(onClickAction);
        }

        public static Task<T> SetOnClickAction<T>(this Button self, Func<GameObject, T> onClickAction) {
            SetupOnClickEventObject(self);
            return self.AddOnClickAction(onClickAction);
        }

        private static void SetupOnClickEventObject(Button self) {
            if (self.onClick != null && self.onClick.GetPersistentEventCount() > 0) {
                Log.w("Overriding existing onClick action in " + self, self.gameObject);
            }
            self.onClick = new Button.ButtonClickedEvent(); // clear previous onClick listeners
        }

        public static Task AddOnClickAction(this Button self, Action<GameObject> onClickAction) {
            return AddOnClickAction(self, (go) => {
                onClickAction(go);
                return true;
            });
        }

        public static Task<T> AddOnClickAction<T>(this Button self, Func<GameObject, T> onClickFunc) {
            onClickFunc.ThrowErrorIfNull("Passed onClickFunc was null");
            var tcs = new TaskCompletionSource<T>();
            var originTrace = new StackTrace();
            Task alreadyRunningTask = null;
            self.onClick.AddListener(() => {
                if (alreadyRunningTask != null && !alreadyRunningTask.IsCompleted) { return; }
                EventBus.instance.Publish(EventConsts.catUi + UiEvents.BUTTON_CLICKED, self);
                try {
                    T res = onClickFunc(self.gameObject);
                    if (res is Task<T> asyncT) {
                        alreadyRunningTask = asyncT;
                        WaitForTaskSuccess(asyncT, originTrace, tcs).ContinueWithSameContext(wasSuccess => {
                            if (wasSuccess.Result) { tcs.TrySetResult(asyncT.Result); }
                        });
                    } else if (res is Task t) {
                        alreadyRunningTask = t;
                        WaitForTaskSuccess(t, originTrace, tcs).ContinueWithSameContext(wasSuccess => {
                            if (wasSuccess.Result) { tcs.TrySetResult((T)(object)t); }
                        });
                    } else {
                        tcs.TrySetResult(res);
                    }
                } catch (Exception e) {
                    Log.e(e);
                    tcs.TrySetException(e);
                }
            });
            return tcs.Task;
        }

        private static async Task<bool> WaitForTaskSuccess<T>(Task task, StackTrace originTrace, TaskCompletionSource<T> tcs) {
            try {
                await task;
                return true;
            } catch (Exception e) {
                Log.e(e);
                if (task.IsCanceled) { tcs.TrySetCanceled(); }
                if (task.IsFaulted) { tcs.TrySetException(task.Exception); }
            }
            return false;
        }

        public static UnityAction<bool> SetOnValueChangedAction(this Toggle self, Func<bool, bool> onValueChanged) {
            if (self.onValueChanged != null && self.onValueChanged.GetPersistentEventCount() > 0) {
                Log.w("Overriding old onValueChanged listener for toggle " + self, self.gameObject);
            }
            self.onValueChanged = new Toggle.ToggleEvent(); // clear previous onValueChanged listeners
            return AddOnValueChangedAction(self, onValueChanged);
        }

        public static UnityAction<bool> AddOnValueChangedAction(this Toggle self, Func<bool, bool> onValueChanged, bool skipChangesByLogic = true) {
            if (onValueChanged != null) {
                var oldIsOn = self.isOn;
                UnityAction<bool> newListener = (newIsOn) => {
                    if (oldIsOn == newIsOn) { return; }
                    // Ignore event event if it was triggered through code, only fire for actual user input:
                    if (skipChangesByLogic && !self.ChangeWasTriggeredByUserThroughEventSystem()) { return; }
                    if (!onValueChanged(newIsOn)) { // Change was rejected, revert UI:
                        self.isOn = oldIsOn;
                    } else { // Change was accepted:
                        oldIsOn = newIsOn;
                        EventBus.instance.Publish(EventConsts.catUi + UiEvents.TOGGLE_CHANGED, self, newIsOn);
                    }
                };
                self.onValueChanged.AddListener(newListener);
                return newListener;
            }
            return null;
        }

        public static UnityAction<float> SetOnValueChangedAction(this Slider self, Func<float, bool> onValueChanged) {
            if (self.onValueChanged != null && self.onValueChanged.GetPersistentEventCount() > 0) {
                Log.w("Overriding old onValueChanged listener for slider " + self, self.gameObject);
            }
            self.onValueChanged = new Slider.SliderEvent(); // clear previous onValueChanged listeners
            return AddOnValueChangedAction(self, onValueChanged);
        }

        public static UnityAction<float> AddOnValueChangedAction(this Slider self, Func<float, bool> onValueChanged, bool skipChangesByLogic = true) {
            if (onValueChanged != null) {
                var oldValue = self.value;
                UnityAction<float> newListener = (newValue) => {
                    if (SameValueAsBefore(oldValue, newValue, self.minValue, self.maxValue)) { return; }
                    // Ignore event event if it was triggered through code, only fire for actual user input:
                    if (skipChangesByLogic && !self.ChangeWasTriggeredByUserThroughEventSystem()) { return; }
                    if (!onValueChanged(newValue)) { // Change was rejected, revert UI:
                        self.value = oldValue;
                    } else { // Change was accepted:
                        oldValue = newValue;
                        EventBus.instance.Publish(EventConsts.catUi + UiEvents.SLIDER_CHANGED, self, newValue);
                    }
                };
                self.onValueChanged.AddListener(newListener);
                return newListener;
            }
            return null;
        }

        private static bool SameValueAsBefore(float oldValue, float newValue, float minValue, float maxValue) {
            var absoluteChange = Mathf.Abs(newValue - oldValue);
            var fullSliderRange = maxValue - minValue;
            var percentageChanged = absoluteChange / fullSliderRange; // Values will be between 0 and 1
            return percentageChanged < 0.01; // If less then 1% change ignore it, UI glitch 
        }

        public static UnityAction<float> SetOnValueChangedActionThrottled(this Slider self, Action<float> onValueChanged, double delayInMs = 200) {
            if (self.onValueChanged != null && self.onValueChanged.GetPersistentEventCount() > 0) {
                Log.w("Overriding old onValueChanged listener for input field " + self, self.gameObject);
            }
            self.onValueChanged = new Slider.SliderEvent(); // clear previous onValueChanged listeners
            return AddOnValueChangedActionThrottled(self, onValueChanged, delayInMs);
        }

        public static UnityAction<float> AddOnValueChangedActionThrottled(this Slider self, Action<float> onValueChanged, double delayInMs = 1000) {
            EventHandler<float> action = (_, newFloat) => { onValueChanged(newFloat); };
            var throttledAction = action.AsThrottledDebounce(delayInMs, skipFirstEvent: true);
            return self.AddOnValueChangedAction((newValue) => {
                throttledAction(self, newValue);
                return true;
            });
        }

        public static bool ChangeWasTriggeredByUserThroughEventSystem(this Component self) {
            return EventSystem.current?.currentSelectedGameObject == self.gameObject;
        }

        /// <summary> Similar to ChangeWasTriggeredByUserThroughEventSystem but checks not only if the 
        /// eventsystems current selected GameObject is the target GO but also if any of the parents match the 
        /// target GO which is important when a child of the UI construct is the one that received the click and 
        /// informed the parent component based on this event (like the DropDown does) </summary>
        private static bool ChangeInChildWasTriggeredByUserThroughEventSystem(this Component self) {
            var go = EventSystem.current?.currentSelectedGameObject;
            return CheckEqualOrParent(self.gameObject, go);
        }

        private static bool CheckEqualOrParent(GameObject parent, GameObject go) {
            if (go == null) { return false; }
            return go == parent || CheckEqualOrParent(parent, go.GetParent());
        }

        public static UnityAction<string> SetOnValueChangedAction(this InputField self, Func<string, bool> onValueChanged) {
            if (self.onValueChanged != null && self.onValueChanged.GetPersistentEventCount() > 0) {
                Log.w("Overriding old onValueChanged listener for input field " + self, self.gameObject);
            }
            self.onValueChanged = new InputField.OnChangeEvent(); // clear previous onValueChanged listeners
            return AddOnValueChangedAction(self, onValueChanged);
        }

        public static UnityAction<string> AddOnValueChangedAction(this InputField self, Func<string, bool> onValueChanged, bool skipChangesByLogic = true) {
            if (onValueChanged != null) {
                var oldText = self.text;
                UnityAction<string> newListener = (newText) => {
                    if (newText == oldText) { return; }
                    // Ignore event event if it was triggered through code, only fire for actual user input:
                    if (skipChangesByLogic && !self.ChangeWasTriggeredByUserThroughEventSystem()) { return; }
                    if (!onValueChanged(newText)) {
                        self.text = oldText;
                    } else {
                        oldText = newText;
                        EventBus.instance.Publish(EventConsts.catUi + UiEvents.INPUTFIELD_CHANGED, self, newText);
                    }
                };
                self.onValueChanged.AddListener(newListener);
                return newListener;
            }
            return null;
        }

        public static UnityAction<int> SetOnValueChangedAction(this Dropdown self, Func<int, bool> onValueChanged) {
            AssertV2.IsNotNull(self, "self (Dropdown)");
            if (self.onValueChanged != null && self.onValueChanged.GetPersistentEventCount() > 0) {
                Log.w("Overriding old onValueChanged listener for input field " + self, self.gameObject);
            }
            self.onValueChanged = new Dropdown.DropdownEvent(); // clear previous onValueChanged listeners
            return AddOnValueChangedAction(self, onValueChanged);
        }

        public static UnityAction<int> AddOnValueChangedAction(this Dropdown self, Func<int, bool> onValueChanged) {
            if (onValueChanged != null) {
                var oldSelection = self.value;
                UnityAction<int> newListener = (newSection) => {
                    if (newSection == oldSelection) { return; }
                    // Ignore event event if it was triggered through code, only fire for actual user input:
                    if (!self.ChangeInChildWasTriggeredByUserThroughEventSystem()) { return; }
                    if (!onValueChanged(newSection)) {
                        self.value = oldSelection;
                    } else {
                        oldSelection = newSection;
                        EventBus.instance.Publish(EventConsts.catUi + UiEvents.DROPDOWN_CHANGED, self, newSection);
                    }
                };
                self.onValueChanged.AddListener(newListener);
                return newListener;
            }
            return null;
        }

        public static UnityAction<string> SetOnValueChangedActionThrottled(this InputField self, Action<string> onValueChanged, double delayInMs = 500) {
            if (self.onValueChanged != null && self.onValueChanged.GetPersistentEventCount() > 0) {
                Log.w("Overriding old onValueChanged listener for input field " + self, self.gameObject);
            }
            self.onValueChanged = new InputField.OnChangeEvent(); // clear previous onValueChanged listeners
            return AddOnValueChangedActionThrottled(self, onValueChanged, delayInMs);
        }

        public static UnityAction<string> AddOnValueChangedActionThrottled(this InputField self, Action<string> onValueChanged, double delayInMs = 500) {
            EventHandler<string> action = (_, newText) => { onValueChanged(newText); };
            var throttledAction = action.AsThrottledDebounce(delayInMs, skipFirstEvent: true);
            return self.AddOnValueChangedAction((newText) => {
                throttledAction(self, newText);
                return true;
            });
        }

        /// <summary> Sets focus on the input field </summary>
        public static void SelectV2(this InputField self) {
            self.Select();
            self.ActivateInputField();
        }

        /// <summary> Sets the input text localized which will notify all UI listeners </summary>
        public static void SetTextLocalizedWithNotify(this InputField self, string text) {
            self.SelectV2(); // Without this the change listeners are not triggered
            self.textLocalized(text);
        }

        private class Wrapper {
            public Action stateChangeListener;
        }

        public static void SubscribeToStateChanges<T, V>(this UnityEngine.Object self, IDataStore<T> store, Func<T, V> getSubState, Action<V> updateUi, bool triggerOnSubscribe = true, bool eventsAlwaysInMainThread = true) {
            updateUi(getSubState(store.GetState()));
            Wrapper w = new Wrapper();
            w.stateChangeListener = store.AddStateChangeListener(getSubState, newVal => {
                if (eventsAlwaysInMainThread) {
                    MainThread.Invoke(() => { SubscribeToStateChanges_OnChanged(self, store, updateUi, w, newVal); });
                } else {
                    SubscribeToStateChanges_OnChanged(self, store, updateUi, w, newVal);
                }
            }, triggerOnSubscribe);
        }

        private static void SubscribeToStateChanges_OnChanged<T, V>(UnityEngine.Object self, IDataStore<T> store, Action<V> updateUi, Wrapper w, V newVal) {
            if (self.IsDestroyed()) {
                store.onStateChanged -= w.stateChangeListener;
                return;
            }
            OnStateChangedForUnity(updateUi, newVal, self);
        }

        private static void OnStateChangedForUnity<V>(Action<V> onStateChanged, V newVal, UnityEngine.Object context) {
            if (context is Behaviour b && !b.isActiveAndEnabled) { return; }
            if (context is GameObject go && !go.activeInHierarchy) { return; }
            onStateChanged(newVal);
        }

        private static void OnSubstateChangedForUnity<V>(SubListeners<V> onStateChanged, V newVal, UnityEngine.Object context) {
            if (context.IsDestroyed()) {
                onStateChanged.UnregisterFromParent();
                return;
            }
            if (context is Behaviour b && !b.isActiveAndEnabled) { return; }
            if (context is GameObject go && !go.activeInHierarchy) { return; }
            onStateChanged.OnSubstateChanged(newVal);
        }

        public static void SubscribeToStateChanges<T>(this InputField self, IDataStore<T> store, Func<T, string> getSubState) {
            self.SubscribeToStateChanges(store, getSubState, newText => self.text = newText);
        }

        public static void SubscribeToStateChanges<T>(this Toggle self, IDataStore<T> store, Func<T, bool> getSubState) {
            self.SubscribeToStateChanges(store, getSubState, newCheckedState => self.isOn = newCheckedState);
        }

        public static void SubscribeToStateChanges<T>(this Slider self, IDataStore<T> store, Func<T, float> getSubState) {
            self.SubscribeToStateChanges(store, getSubState, newText => self.value = newText);
        }

        public static SubListeners<S> NewSubStateListenerForUnity<T, S>(this IDataStore<T> self, UnityEngine.Object context, Func<T, S> getSubState, bool eventsAlwaysInMainThread = true) {
            var subListener = new SubListeners<S>(getSubState(self.GetState()));
            var ownListenerInParent = self.AddStateChangeListener(getSubState, newSubState => {
                if (eventsAlwaysInMainThread) {
                    MainThread.Invoke(() => { OnSubstateChangedForUnity(subListener, newSubState, context); });
                } else {
                    OnSubstateChangedForUnity(subListener, newSubState, context);
                }
            });
            subListener.SetUnregisterInParentAction(() => { self.onStateChanged -= ownListenerInParent; });
            return subListener;
        }

        public static SubListeners<S> NewSubStateListenerForUnity<T, S>(this SubListeners<T> self, UnityEngine.Object context, Func<T, S> getSubState, bool eventsAlwaysInMainThread = true) {
            var subListener = new SubListeners<S>(getSubState(self.latestSubState));
            var ownListenerInParent = self.AddStateChangeListener(getSubState, newSubState => {
                if (eventsAlwaysInMainThread) {
                    MainThread.Invoke(() => { OnSubstateChangedForUnity(subListener, newSubState, context); });
                } else {
                    OnSubstateChangedForUnity(subListener, newSubState, context);
                }
            });
            subListener.SetUnregisterInParentAction(() => { self.innerListeners -= ownListenerInParent; });
            return subListener;
        }

        public static int CalcCurrentMaxSortingOrderInLayer(this Canvas self) {
            var l = ResourcesV2.FindAllInScene<Canvas>().Filter(x => {
                if (x == self) { return false; } // Skip the input canvas
                if (!x.enabled) { return false; } // Only include currently active ones
                if (!x.gameObject.activeInHierarchy) { return false; }
                if (x.sortingLayerID != self.sortingLayerID) { return false; }
                var o = x.GetComponentV2<CanvasOrderOnTop>();
                if (o != null && o.excludeFromOrderCalc) { return false; }
                if (o != null && o.HasComponent<IgnoreRootCanvas>(out var _)) { return true; }
                return true;
            });
            return l.Max(x => x.sortingOrder);
        }

    }

    public static class UiButtonExtensions {

        public static void SetNormalColor(this Button self, Color normalColor) {
            var c = self.colors;
            c.normalColor = normalColor;
            self.colors = c;
        }

    }

}