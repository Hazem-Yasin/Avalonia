﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using Avalonia.Browser.Interop;
using Avalonia.Browser.Skia;
using Avalonia.Collections.Pooled;
using Avalonia.Controls;
using Avalonia.Controls.Embedding;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.TextInput;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using SkiaSharp;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace Avalonia.Browser
{
    public class AvaloniaView : ITextInputMethodImpl
    {
        private static readonly PooledList<RawPointerPoint> s_intermediatePointsPooledList = new(ClearMode.Never);
        private readonly BrowserTopLevelImpl _topLevelImpl;
        private EmbeddableControlRoot _topLevel;

        private readonly JSObject _containerElement;
        private readonly JSObject _inputElement;
        private TextInputMethodClient? _client;

        /// <param name="divId">ID of the html element where avalonia content should be rendered.</param>
        public AvaloniaView(string divId)
            : this(DomHelper.GetElementById(divId) ?? throw new Exception($"Element with id '{divId}' was not found in the html document."))
        {
        }

        public AvaloniaView(JSObject host)
        {
            var hostContent = DomHelper.CreateAvaloniaHost(host);
            if (hostContent == null)
            {
                throw new InvalidOperationException("Avalonia WASM host wasn't initialized.");
            }

            _containerElement = host;
            var nativeControlsContainer = hostContent.GetPropertyAsJSObject("nativeHost")
                                       ?? throw new InvalidOperationException("NativeHost cannot be null");
            _inputElement = hostContent.GetPropertyAsJSObject("inputElement")
                            ?? throw new InvalidOperationException("InputElement cannot be null");

            _topLevelImpl = new BrowserTopLevelImpl(_containerElement, nativeControlsContainer, this);
            _topLevelImpl.SetCssCursor = (cursor) =>
            {
                InputHelper.SetCursor(_containerElement, cursor);
            };

            _topLevel = new EmbeddableControlRoot(_topLevelImpl);
            _topLevel.Prepare();

            InputHelper.InitializeBackgroundHandlers();

            InputHelper.SubscribeKeyEvents(
                _containerElement,
                OnKeyDown,
                OnKeyUp);

            InputHelper.SubscribeTextEvents(
                _inputElement,
                OnBeforeInput,
                OnCompositionStart,
                OnCompositionUpdate,
                OnCompositionEnd);

            InputHelper.SubscribePointerEvents(_containerElement, OnPointerMove, OnPointerDown, OnPointerUp,
                OnPointerCancel, OnWheel);

            InputHelper.SubscribeDropEvents(_containerElement, OnDragEvent);
            InputHelper.FocusElement(_containerElement);

            _topLevel.Renderer.Start();
            _topLevel.RequestAnimationFrame(_ =>
            {
                // Try to get local splash-screen of the specific host.
                // If couldn't find - get global one by ID for compatibility.
                var splash = DomHelper.GetElementsByClassName("avalonia-splash", host)
                    ?? DomHelper.GetElementById("avalonia-splash"); 
                if (splash is not null)
                {
                    DomHelper.AddCssClass(splash, "splash-close");
                    splash.Dispose();
                }
            });
        }

        private static RawPointerPoint ExtractRawPointerFromJSArgs(JSObject args)
        {
            var point = new RawPointerPoint
            {
                Position = new Point(args.GetPropertyAsDouble("offsetX"), args.GetPropertyAsDouble("offsetY")),
                Pressure = (float)args.GetPropertyAsDouble("pressure"),
                XTilt = (float)args.GetPropertyAsDouble("tiltX"),
                YTilt = (float)args.GetPropertyAsDouble("tiltY"),
                Twist = (float)args.GetPropertyAsDouble("twist")
            };

            return point;
        }

        private bool OnPointerMove(JSObject args)
        {
            var pointerType = args.GetPropertyAsString("pointerType");
            var point = ExtractRawPointerFromJSArgs(args);
            var type = pointerType switch
            {
                "touch" => RawPointerEventType.TouchUpdate,
                _ => RawPointerEventType.Move
            };

            var coalescedEvents = new Lazy<IReadOnlyList<RawPointerPoint>?>(() =>
            {
                var points = InputHelper.GetCoalescedEvents(args);
                s_intermediatePointsPooledList.Clear();
                s_intermediatePointsPooledList.Capacity = points.Length - 1;

                // Skip the last one, as it is already processed point.
                for (var i = 0; i < points.Length - 1; i++)
                {
                    var point = points[i];
                    s_intermediatePointsPooledList.Add(ExtractRawPointerFromJSArgs(point));
                }

                return s_intermediatePointsPooledList;
            });

            return _topLevelImpl.RawPointerEvent(type, pointerType!, point, GetModifiers(args), args.GetPropertyAsInt32("pointerId"), coalescedEvents);
        }

        private bool OnPointerDown(JSObject args)
        {
            var pointerType = args.GetPropertyAsString("pointerType") ?? "mouse";
            var type = pointerType switch
            {
                "touch" => RawPointerEventType.TouchBegin,
                _ => args.GetPropertyAsInt32("button") switch
                {
                    0 => RawPointerEventType.LeftButtonDown,
                    1 => RawPointerEventType.MiddleButtonDown,
                    2 => RawPointerEventType.RightButtonDown,
                    3 => RawPointerEventType.XButton1Down,
                    4 => RawPointerEventType.XButton2Down,
                    5 => RawPointerEventType.XButton1Down, // should be pen eraser button,
                    _ => RawPointerEventType.Move
                }
            };

            var point = ExtractRawPointerFromJSArgs(args);
            return _topLevelImpl.RawPointerEvent(type, pointerType, point, GetModifiers(args), args.GetPropertyAsInt32("pointerId"));
        }

        private bool OnPointerUp(JSObject args)
        {
            var pointerType = args.GetPropertyAsString("pointerType") ?? "mouse";
            var type = pointerType switch
            {
                "touch" => RawPointerEventType.TouchEnd,
                _ => args.GetPropertyAsInt32("button") switch
                {
                    0 => RawPointerEventType.LeftButtonUp,
                    1 => RawPointerEventType.MiddleButtonUp,
                    2 => RawPointerEventType.RightButtonUp,
                    3 => RawPointerEventType.XButton1Up,
                    4 => RawPointerEventType.XButton2Up,
                    5 => RawPointerEventType.XButton1Up, // should be pen eraser button,
                    _ => RawPointerEventType.Move
                }
            };

            var point = ExtractRawPointerFromJSArgs(args);
            return _topLevelImpl.RawPointerEvent(type, pointerType, point, GetModifiers(args), args.GetPropertyAsInt32("pointerId"));
        }
        
        private bool OnPointerCancel(JSObject args)
        {
            var pointerType = args.GetPropertyAsString("pointerType") ?? "mouse";
            if (pointerType == "touch")
            {
                var point = ExtractRawPointerFromJSArgs(args);
                _topLevelImpl.RawPointerEvent(RawPointerEventType.TouchCancel, pointerType, point,
                    GetModifiers(args), args.GetPropertyAsInt32("pointerId"));
            }

            return false;
        }

        private bool OnWheel(JSObject args)
        {
            return _topLevelImpl.RawMouseWheelEvent(new Point(args.GetPropertyAsDouble("offsetX"), args.GetPropertyAsDouble("offsetY")),
                new Vector(-(args.GetPropertyAsDouble("deltaX") / 50), -(args.GetPropertyAsDouble("deltaY") / 50)), GetModifiers(args));
        }

        private static RawInputModifiers GetModifiers(JSObject e)
        {
            var modifiers = RawInputModifiers.None;

            if (e.GetPropertyAsBoolean("ctrlKey"))
                modifiers |= RawInputModifiers.Control;
            if (e.GetPropertyAsBoolean("altKey"))
                modifiers |= RawInputModifiers.Alt;
            if (e.GetPropertyAsBoolean("shiftKey"))
                modifiers |= RawInputModifiers.Shift;
            if (e.GetPropertyAsBoolean("metaKey"))
                modifiers |= RawInputModifiers.Meta;

            var buttons = e.GetPropertyAsInt32("buttons");
            if ((buttons & 1L) == 1)
                modifiers |= RawInputModifiers.LeftMouseButton;

            if ((buttons & 2L) == 2)
                modifiers |= e.GetPropertyAsString("type") == "pen" ? RawInputModifiers.PenBarrelButton : RawInputModifiers.RightMouseButton;

            if ((buttons & 4L) == 4)
                modifiers |= RawInputModifiers.MiddleMouseButton;

            if ((buttons & 8L) == 8)
                modifiers |= RawInputModifiers.XButton1MouseButton;

            if ((buttons & 16L) == 16)
                modifiers |= RawInputModifiers.XButton2MouseButton;

            if ((buttons & 32L) == 32)
                modifiers |= RawInputModifiers.PenEraser;

            return modifiers;
        }

        public bool OnDragEvent(JSObject args)
        {
            var eventType = args?.GetPropertyAsString("type") switch
            {
                "dragenter" => RawDragEventType.DragEnter,
                "dragover" => RawDragEventType.DragOver,
                "dragleave" => RawDragEventType.DragLeave,
                "drop" => RawDragEventType.Drop,
                _ => (RawDragEventType)(int)-1
            };
            var dataObject = args?.GetPropertyAsJSObject("dataTransfer");
            if (args is null || eventType < 0 || dataObject is null)
            {
                return false;
            }

            // If file is dropped, we need storage js to be referenced.
            // TODO: restructure JS files, so it's not needed.
            _ = AvaloniaModule.ImportStorage();

            var position = new Point(args.GetPropertyAsDouble("offsetX"), args.GetPropertyAsDouble("offsetY"));
            var modifiers = GetModifiers(args);

            var effectAllowedStr = dataObject.GetPropertyAsString("effectAllowed") ?? "none";
            var effectAllowed = DragDropEffects.None;
            if (effectAllowedStr.Contains("copy", StringComparison.OrdinalIgnoreCase))
            {
                effectAllowed |= DragDropEffects.Copy;
            }
            if (effectAllowedStr.Contains("link", StringComparison.OrdinalIgnoreCase))
            {
                effectAllowed |= DragDropEffects.Link;
            }
            if (effectAllowedStr.Contains("move", StringComparison.OrdinalIgnoreCase))
            {
                effectAllowed |= DragDropEffects.Move;
            }
            if (effectAllowedStr.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                effectAllowed |= DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link;
            }
            if (effectAllowed == DragDropEffects.None)
            {
                return false;
            }

            var dropEffect = _topLevelImpl.RawDragEvent(eventType, position, modifiers, new BrowserDataObject(dataObject), effectAllowed);
            dataObject.SetProperty("dropEffect", dropEffect.ToString().ToLowerInvariant());

            return eventType is RawDragEventType.Drop or RawDragEventType.DragOver
                   && dropEffect != DragDropEffects.None;
        }
        
        private bool OnKeyDown (string code, string key, int modifier)
        {
            var handled = _topLevelImpl.RawKeyboardEvent(RawKeyEventType.KeyDown, code, key, (RawInputModifiers)modifier);

            if (!handled && key.Length == 1)
            {
                handled = _topLevelImpl.RawTextEvent(key);
            }

            return handled;
        }

        private bool OnKeyUp(string code, string key, int modifier)
        {
            return _topLevelImpl.RawKeyboardEvent(RawKeyEventType.KeyUp, code, key, (RawInputModifiers)modifier);
        }

        private bool OnBeforeInput(JSObject arg, int start, int end)
        {
            var type = arg.GetPropertyAsString("inputType");
            if (type != "deleteByComposition")
            {
                if (type == "deleteContentBackward")
                {
                    start = _inputElement.GetPropertyAsInt32("selectionStart");
                    end = _inputElement.GetPropertyAsInt32("selectionEnd");
                }
                else
                {
                    start = -1;
                    end = -1;
                }
            }

            if(start != -1 && end != -1 && _client != null)
            {
                _client.Selection = new TextSelection(start, end);
            }
            return false;
        }

        private bool OnCompositionStart (JSObject args)
        {
            if (_client == null)
                return false;

            _client.SetPreeditText(null);
            IsComposing = true;

            return false;
        }

        private bool OnCompositionUpdate(JSObject args)
        {
            if (_client == null)
                return false;

            _client.SetPreeditText(args.GetPropertyAsString("data"));

            return false;
        }

        private bool OnCompositionEnd(JSObject args)
        {
            if (_client == null)
                return false;

            IsComposing = false;

            _client.SetPreeditText(null);

            var text = args.GetPropertyAsString("data");

            if(text != null)
            {
                return _topLevelImpl.RawTextEvent(text);
            }

            return false;
        }

        public Control? Content
        {
            get => (Control)_topLevel.Content!;
            set => _topLevel.Content = value;
        }

        public bool IsComposing { get; private set; }

        private void HideIme()
        {
            InputHelper.HideElement(_inputElement);
            InputHelper.FocusElement(_containerElement);
        }

        void ITextInputMethodImpl.SetClient(TextInputMethodClient? client)
        {
            if (_client != null)
            {
                _client.SurroundingTextChanged -= SurroundingTextChanged;
            }

            if (client != null)
            {
                client.SurroundingTextChanged += SurroundingTextChanged;
            }

            InputHelper.ClearInputElement(_inputElement);

            _client = client;

            if (_client != null)
            {
                InputHelper.ShowElement(_inputElement);
                InputHelper.FocusElement(_inputElement);

                var surroundingText = _client.SurroundingText ?? "";
                var selection = _client.Selection;

                InputHelper.SetSurroundingText(_inputElement, surroundingText, selection.Start, selection.End);
            }
            else
            {
                HideIme();
            }
        }

        private void SurroundingTextChanged(object? sender, EventArgs e)
        {
            if (_client != null)
            {
                var surroundingText = _client.SurroundingText ?? "";
                var selection = _client.Selection;

                InputHelper.SetSurroundingText(_inputElement, surroundingText, selection.Start, selection.End);
            }
        }

        void ITextInputMethodImpl.SetCursorRect(Rect rect)
        {
            InputHelper.FocusElement(_inputElement);
            InputHelper.SetBounds(_inputElement, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, _client?.Selection.End ?? 0);
            InputHelper.FocusElement(_inputElement);
        }

        void ITextInputMethodImpl.SetOptions(TextInputOptions options)
        {
        }

        void ITextInputMethodImpl.Reset()
        {
            InputHelper.ClearInputElement(_inputElement);
            InputHelper.SetSurroundingText(_inputElement, "", 0, 0);
        }
    }
}
