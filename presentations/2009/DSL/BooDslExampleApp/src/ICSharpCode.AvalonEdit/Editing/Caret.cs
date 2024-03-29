// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision: 4901 $</version>
// </file>

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Windows.Threading;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace ICSharpCode.AvalonEdit.Editing
{
	/// <summary>
	/// Helper class with caret-related methods.
	/// </summary>
	public sealed class Caret
	{
		readonly TextArea textArea;
		readonly TextView textView;
		readonly CaretLayer caretAdorner;
		bool visible;
		
		internal Caret(TextArea textArea)
		{
			this.textArea = textArea;
			this.textView = textArea.TextView;
			position = new TextViewPosition(1, 1, 0);
			
			caretAdorner = new CaretLayer(textView);
			textView.InsertLayer(caretAdorner, KnownLayer.Caret, LayerInsertionPosition.Replace);
			textView.VisualLinesChanged += TextView_VisualLinesChanged;
			textView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
		}
		
		void TextView_VisualLinesChanged(object sender, EventArgs e)
		{
			if (visible) {
				Show();
			}
			// required because the visual columns might have changed if the
			// element generators did something differently than on the last run
			// (e.g. a FoldingSection was collapsed)
			InvalidateVisualColumn();
		}
		
		void TextView_ScrollOffsetChanged(object sender, EventArgs e)
		{
			if (caretAdorner != null) {
				caretAdorner.InvalidateVisual();
			}
		}
		
		double desiredXPos = double.NaN;
		TextViewPosition position;
		
		/// <summary>
		/// Gets/Sets the position of the caret.
		/// Retrieving this property will validate the visual column (which can be expensive).
		/// Use the <see cref="Location"/> property instead if you don't need the visual column.
		/// </summary>
		public TextViewPosition Position {
			get {
				ValidateVisualColumn();
				return position;
			}
			set {
				if (position != value) {
					position = value;
					
					storedCaretOffset = -1;
					
					//Debug.WriteLine("Caret position changing to " + value);
					
					ValidatePosition();
					InvalidateVisualColumn();
					if (PositionChanged != null) {
						PositionChanged(this, EventArgs.Empty);
					}
					Debug.WriteLine("Caret position changed to " + value);
					if (visible)
						Show();
				}
			}
		}
		
		/// <summary>
		/// Gets/Sets the location of the caret.
		/// The getter of this property is faster than <see cref="Position"/> because it doesn't have
		/// to validate the visual column.
		/// </summary>
		public TextLocation Location {
			get {
				return position;
			}
			set {
				this.Position = new TextViewPosition(value);
			}
		}
		
		/// <summary>
		/// Gets/Sets the caret line.
		/// </summary>
		public int Line {
			get { return position.Line; }
			set {
				this.Position = new TextViewPosition(value, position.Column);
			}
		}
		
		/// <summary>
		/// Gets/Sets the caret column.
		/// </summary>
		public int Column {
			get { return position.Column; }
			set {
				this.Position = new TextViewPosition(position.Line, value);
			}
		}
		
		/// <summary>
		/// Gets/Sets the caret visual column.
		/// </summary>
		public int VisualColumn {
			get {
				ValidateVisualColumn();
				return position.VisualColumn;
			}
			set {
				this.Position = new TextViewPosition(position.Line, position.Column, value);
			}
		}
		
		int storedCaretOffset;
		
		internal void OnDocumentChanging()
		{
			storedCaretOffset = this.Offset;
			InvalidateVisualColumn();
		}
		
		internal void OnDocumentChanged(DocumentChangeEventArgs e)
		{
			InvalidateVisualColumn();
			if (storedCaretOffset >= 0) {
				int newCaretOffset = e.GetNewOffset(storedCaretOffset, AnchorMovementType.AfterInsertion);
				TextDocument document = textArea.Document;
				if (document != null) {
					// keep visual column
					this.Position = new TextViewPosition(document.GetLocation(newCaretOffset), position.VisualColumn);
				}
			}
			storedCaretOffset = -1;
		}
		
		/// <summary>
		/// Gets/Sets the caret offset.
		/// Setting the caret offset has the side effect of setting the <see cref="DesiredXPos"/> to NaN.
		/// </summary>
		public int Offset {
			get {
				TextDocument document = textArea.Document;
				if (document == null) {
					return 0;
				} else {
					return document.GetOffset(position);
				}
			}
			set {
				TextDocument document = textArea.Document;
				if (document != null) {
					this.Position = new TextViewPosition(document.GetLocation(value));
					this.DesiredXPos = double.NaN;
				}
			}
		}
		
		/// <summary>
		/// Gets/Sets the desired x-position of the caret, in device-independent pixels.
		/// This property is NaN if the caret has no desired position.
		/// </summary>
		public double DesiredXPos {
			get { return desiredXPos; }
			set { desiredXPos = value; }
		}
		
		void ValidatePosition()
		{
			if (position.Line < 1)
				position.Line = 1;
			if (position.Column < 1)
				position.Column = 1;
			if (position.VisualColumn < -1)
				position.VisualColumn = -1;
			TextDocument document = textArea.Document;
			if (document != null) {
				if (position.Line > document.LineCount) {
					position.Line = document.LineCount;
					position.Column = document.GetLineByNumber(position.Line).Length + 1;
					position.VisualColumn = -1;
				} else {
					DocumentLine line = document.GetLineByNumber(position.Line);
					if (position.Column > line.Length + 1) {
						position.Column = line.Length + 1;
						position.VisualColumn = -1;
					}
				}
			}
		}
		
		/// <summary>
		/// Event raised when the caret position has changed.
		/// This event might be raised multiple times during a big update operation.
		/// You might want to check TextDocument.IsInUpdate and delay time-consuming
		/// actions until the update operation ends.
		/// TODO: only raise this event outside of document updates
		/// </summary>
		public event EventHandler PositionChanged;
		
		bool visualColumnValid;
		
		void ValidateVisualColumn()
		{
			if (!visualColumnValid) {
				TextDocument document = textArea.Document;
				if (document != null) {
					var documentLine = document.GetLineByNumber(position.Line);
					RevalidateVisualColumn(textView.GetOrConstructVisualLine(documentLine));
				}
			}
		}
		
		void InvalidateVisualColumn()
		{
			visualColumnValid = false;
		}
		
		/// <summary>
		/// Validates the visual column of the caret using the specified visual line.
		/// The visual line must contain the caret offset.
		/// </summary>
		void RevalidateVisualColumn(VisualLine visualLine)
		{
			if (visualLine == null)
				throw new ArgumentNullException("visualLine");
			
			// mark column as validated
			visualColumnValid = true;
			
			int caretOffset = textView.Document.GetOffset(position);
			int firstDocumentLineOffset = visualLine.FirstDocumentLine.Offset;
			if (position.VisualColumn < 0) {
				position.VisualColumn = visualLine.GetVisualColumn(caretOffset - firstDocumentLineOffset);
			} else {
				int offsetFromVisualColumn = visualLine.GetRelativeOffset(position.VisualColumn);
				offsetFromVisualColumn += firstDocumentLineOffset;
				if (offsetFromVisualColumn != caretOffset) {
					position.VisualColumn = visualLine.GetVisualColumn(caretOffset - firstDocumentLineOffset);
				} else {
					if (position.VisualColumn > visualLine.VisualLength) {
						position.VisualColumn = visualLine.VisualLength;
					}
				}
			}
			// search possible caret position (first try forwards)
			int newVisualColumn = visualLine.GetNextCaretPosition(position.VisualColumn - 1, LogicalDirection.Forward, CaretPositioningMode.Normal);
			if (newVisualColumn < 0) {
				// then try backwards
				newVisualColumn = visualLine.GetNextCaretPosition(position.VisualColumn + 1, LogicalDirection.Backward, CaretPositioningMode.Normal);
			}
			if (newVisualColumn < 0)
				throw ThrowUtil.NoValidCaretPosition();
			if (newVisualColumn != position.VisualColumn) {
				int newOffset = visualLine.GetRelativeOffset(newVisualColumn) + firstDocumentLineOffset;
				this.Position = new TextViewPosition(textView.Document.GetLocation(newOffset), newVisualColumn);
			}
		}
		
		Rect CalcCaretRectangle(VisualLine visualLine)
		{
			if (!visualColumnValid) {
				RevalidateVisualColumn(visualLine);
			}
			
			TextLine textLine = visualLine.GetTextLine(position.VisualColumn);
			double xPos = textLine.GetDistanceFromCharacterHit(new CharacterHit(position.VisualColumn, 0));
			double lineTop = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop);
			double lineBottom = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.LineBottom);
			
			return new Rect(xPos,
			                lineTop,
			                SystemParameters.CaretWidth,
			                lineBottom - lineTop);
		}
		
		/// <summary>
		/// Minimum distance of the caret to the view border.
		/// </summary>
		internal const double MinimumDistanceToViewBorder = 30;
		
		/// <summary>
		/// Scrolls the text view so that the caret is visible.
		/// </summary>
		public void BringCaretToView()
		{
			BringCaretToView(MinimumDistanceToViewBorder);
		}
		
		internal void BringCaretToView(double border)
		{
			if (textView != null) {
				VisualLine visualLine = textView.GetOrConstructVisualLine(textView.Document.GetLineByNumber(position.Line));
				Rect caretRectangle = CalcCaretRectangle(visualLine);
				caretRectangle.Inflate(border, border);
				textView.MakeVisible(caretRectangle);
			}
		}
		
		/// <summary>
		/// Makes the caret visible and updates its on-screen position.
		/// </summary>
		public void Show()
		{
			Debug.WriteLine("Caret.Show()");
			visible = true;
			if (!showScheduled) {
				showScheduled = true;
				textArea.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(ShowInternal));
			}
		}
		
		bool showScheduled;
		bool hasWin32Caret;
		
		void ShowInternal()
		{
			showScheduled = false;
			
			// if show was scheduled but caret hidden in the meantime
			if (!visible)
				return;
			
			if (caretAdorner != null && textView != null) {
				VisualLine visualLine = textView.GetVisualLine(position.Line);
				if (visualLine != null) {
					Rect caretRect = CalcCaretRectangle(visualLine);
					// Create Win32 caret so that Windows knows where our managed caret is. This is necessary for
					// features like 'Follow text editing' in the Windows Magnifier.
					if (!hasWin32Caret) {
						hasWin32Caret = Win32.CreateCaret(textView, caretRect.Size);
					}
					if (hasWin32Caret) {
						Win32.SetCaretPosition(textView, caretRect.Location - textView.ScrollOffset);
					}
					caretAdorner.Show(caretRect);
				} else {
					caretAdorner.Hide();
				}
			}
		}
		
		/// <summary>
		/// Makes the caret invisible.
		/// </summary>
		public void Hide()
		{
			Debug.WriteLine("Caret.Hide()");
			visible = false;
			if (hasWin32Caret) {
				Win32.DestroyCaret();
				hasWin32Caret = false;
			}
			if (caretAdorner != null) {
				caretAdorner.Hide();
			}
		}
		
		/// <summary>
		/// Gets/Sets the color of the caret.
		/// </summary>
		public Brush CaretBrush {
			get { return caretAdorner.CaretBrush; }
			set { caretAdorner.CaretBrush = value; }
		}
	}
}
