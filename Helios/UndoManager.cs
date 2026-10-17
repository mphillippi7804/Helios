//  Copyright 2014 Craig Courtney
//    
//  Helios is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  Helios is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace GadrocsWorkshop.Helios
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using GadrocsWorkshop.Helios.ComponentModel;

    public class UndoManager
    {
        private static int MAX_UNDO_ITEMS = 2000;
        private UndoManagerBatch _batch;

        private Stack<IUndoItem> _undoEvents;
        private Stack<IUndoItem> _redoEvents;

        /// <summary>
        /// fired when the current undo stack reaches the empty state
        /// </summary>
        public event EventHandler Empty;

        /// <summary>
        /// fired when the current undo stack leaes the empty state
        /// </summary>
        public event EventHandler NonEmpty;

        public UndoManager()
        {
            _undoEvents = new Stack<IUndoItem>();
            _redoEvents = new Stack<IUndoItem>();
        }

        /// <summary>
        /// true if the undo manager is currently applying undos, can be used
        /// in property change handlers to ignore related changes that will be undone also
        /// </summary>
        public bool Working { get; private set; }

        /// <summary>
        ///  Gets a value indicating whether there is anything that can be undone.
        /// </summary>
        public bool CanUndo => _undoEvents.Count > 0 && !Working && _batch == null;

        /// <summary>
        /// Gets a value indicating whether there is anything that can be rolled forward.
        /// </summary>
        public bool CanRedo => _redoEvents.Count > 0 && !Working && _batch == null;

        /// <summary>
        /// Gets a value indicating whether a batch is currently open, so callers that want to group
        /// several changes into one undo step know whether they need to open (and close) their own.
        /// </summary>
        public bool InBatch => _batch != null;

        /// <summary>
        /// Rollback the last command.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            Working = true;
            IUndoItem undo = _undoEvents.Pop();
            bool nowEmpty = !_undoEvents.Any();
            undo.Undo();
            _redoEvents.Push(undo);
            Working = false;

            if (nowEmpty)
            {
                Empty?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Rollback the last undone command.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
            {
                return;
            }

            Working = true;
            IUndoItem redo = _redoEvents.Pop();
            redo.Do();
            bool emptyBefore = !_undoEvents.Any();
            _undoEvents.Push(redo);
            Working = false;
            if (emptyBefore)
            {
                NonEmpty?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clear the undo history.
        /// WARNING: does not drop the current batch, XXX which seems like an error.
        /// </summary>
        public void ClearUndoHistory()
        {
            // remember if we actually had entries before
            bool nonEmptyBefore = _undoEvents.Any();

            // clear the stack
            _undoEvents.Clear();

            // fire event if this was a change
            if (nonEmptyBefore)
            {
                Empty?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clear the redo history.
        /// </summary>
        public void ClearRedoHistory()
        {
            _redoEvents.Clear();
        }

        /// <summary>
        /// Clear all the undo and redo history.
        /// </summary>
        public void ClearHistory()
        {
            ClearRedoHistory();
            ClearUndoHistory();
        }

        public void AddUndoItem(IUndoItem undoEvent)
        {
            if (Working)
            {
                return;
            }

            bool emptyBefore = (_batch?.Count ?? 0) == 0 && !_undoEvents.Any();
            if (_batch != null)
            {
                _batch.Add(undoEvent);
            }
            else
            {
                ClearRedoHistory();
                _undoEvents.Push(undoEvent);
                if (_undoEvents.Count > MAX_UNDO_ITEMS)
                {
                    // XXX: we throw away the current item? this should be a Dequeue so we can pop the front
                    _undoEvents.Pop();
                }
            }

            if (emptyBefore)
            {
                // no longer empty
                NonEmpty?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AddPropertyChange(object source, string propertyName, object oldValue, object newValue)
        {
            if (Working)
            {
                return;
            }

            Type type = source.GetType();
            PropertyInfo property = type.GetProperty(propertyName);
            if (property?.CanWrite ?? false)
            {
                AddUndoItem(new PropertyChangedUndoItem(source, property, oldValue, newValue));
            }
        }

        public void AddPropertyChange(object sender, PropertyNotificationEventArgs notification)
        {
            if (notification == null || !notification.IsUndoable)
            {
                return;
            }

            while (notification.HasChildNotification)
            {
                notification = notification.ChildNotification;
            }
            AddPropertyChange(notification.EventSource, notification.PropertyName, notification.OldValue, notification.NewValue);
        }

        public void StartBatch()
        {
            if (_batch != null)
            {
                return;
            }

            ClearRedoHistory();
            _batch = new UndoManagerBatch();
        }

        public void CloseBatch()
        {
            if (_batch == null)
            {
                return;
            }

            if (_batch.Count > 0)
            {
                bool emptyBefore = !_undoEvents.Any();
                _undoEvents.Push(_batch);
                if (emptyBefore)
                {
                    // no longer empty
                    NonEmpty?.Invoke(this, EventArgs.Empty);
                }
            }
            _batch = null;
        }

        /// <summary>
        /// roll back everything in the currently open batch, not redoable
        /// </summary>
        public void UndoBatch()
        {
            Working = true;
            bool nonEmptyBefore = (_batch?.Count ?? 0) > 0 || _undoEvents.Any();
            try
            {
                _batch?.Undo();
            }
            finally
            {
                bool nowEmpty = !_undoEvents.Any();
                Working = false;
                _batch = null;
                if (nonEmptyBefore && nowEmpty)
                {
                    Empty?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}
