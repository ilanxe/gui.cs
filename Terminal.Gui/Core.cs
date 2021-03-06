//
// Core.cs: The core engine for gui.cs
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Pending:
//   - Check for NeedDisplay on the hierarchy and repaint
//   - Layout support
//   - "Colors" type or "Attributes" type?
//   - What to surface as "BackgroundCOlor" when clearing a window, an attribute or colors?
//
// Optimziations
//   - Add rendering limitation to the exposed area
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using NStack;
using System.ComponentModel;

namespace Terminal.Gui {

	/// <summary>
	/// Responder base class implemented by objects that want to participate on keyboard and mouse input.
	/// </summary>
	public class Responder {
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Responder"/> can focus.
		/// </summary>
		/// <value><c>true</c> if can focus; otherwise, <c>false</c>.</value>
		public virtual bool CanFocus { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Responder"/> has focus.
		/// </summary>
		/// <value><c>true</c> if has focus; otherwise, <c>false</c>.</value>
		public virtual bool HasFocus { get; internal set; }

		// Key handling
		/// <summary>
		///   This method can be overwritten by view that
		///     want to provide accelerator functionality
		///     (Alt-key for example).
		/// </summary>
		/// <remarks>
		///   <para>
		///     Before keys are sent to the subview on the
		///     current view, all the views are
		///     processed and the key is passed to the widgets
		///     to allow some of them to process the keystroke
		///     as a hot-key. </para>
		///  <para>
		///     For example, if you implement a button that
		///     has a hotkey ok "o", you would catch the
		///     combination Alt-o here.  If the event is
		///     caught, you must return true to stop the
		///     keystroke from being dispatched to other
		///     views.
		///  </para>
		/// </remarks>

		public virtual bool ProcessHotKey (KeyEvent kb)
		{
			return false;
		}

		/// <summary>
		///   If the view is focused, gives the view a
		///   chance to process the keystroke.
		/// </summary>
		/// <remarks>
		///   <para>
		///     Views can override this method if they are
		///     interested in processing the given keystroke.
		///     If they consume the keystroke, they must
		///     return true to stop the keystroke from being
		///     processed by other widgets or consumed by the
		///     widget engine.    If they return false, the
		///     keystroke will be passed using the ProcessColdKey
		///     method to other views to process.
		///   </para>
		///   <para>
		///     The View implementation does nothing but return false,
		///     so it is not necessary to call base.ProcessKey if you
		///     derive directly from View, but you should if you derive
		///     other View subclasses.
		///   </para>
		/// </remarks>
		/// <param name="keyEvent">Contains the details about the key that produced the event.</param>
		public virtual bool ProcessKey (KeyEvent keyEvent)
		{
			return false;
		}

		/// <summary>
		///   This method can be overwritten by views that
		///     want to provide accelerator functionality
		///     (Alt-key for example), but without
		///     interefering with normal ProcessKey behavior.
		/// </summary>
		/// <remarks>
		///   <para>
		///     After keys are sent to the subviews on the
		///     current view, all the view are
		///     processed and the key is passed to the views
		///     to allow some of them to process the keystroke
		///     as a cold-key. </para>
		///  <para>
		///    This functionality is used, for example, by
		///    default buttons to act on the enter key.
		///    Processing this as a hot-key would prevent
		///    non-default buttons from consuming the enter
		///    keypress when they have the focus.
		///  </para>
		/// </remarks>
		/// <param name="keyEvent">Contains the details about the key that produced the event.</param>
		public virtual bool ProcessColdKey (KeyEvent keyEvent)
		{
			return false;
		}

		/// <summary>
		/// Method invoked when a key is pressed.
		/// </summary>
		/// <param name="keyEvent">Contains the details about the key that produced the event.</param>
		/// <returns>true if the event was handled</returns>
		public virtual bool OnKeyDown (KeyEvent keyEvent)
		{
			return false;
		}

		/// <summary>
		/// Method invoked when a key is released.
		/// </summary>
		/// <param name="keyEvent">Contains the details about the key that produced the event.</param>
		/// <returns>true if the event was handled</returns>
		public virtual bool OnKeyUp (KeyEvent keyEvent)
		{
			return false;
		}


		/// <summary>
		/// Method invoked when a mouse event is generated
		/// </summary>
		/// <returns><c>true</c>, if the event was handled, <c>false</c> otherwise.</returns>
		/// <param name="mouseEvent">Contains the details about the mouse event.</param>
		public virtual bool MouseEvent (MouseEvent mouseEvent)
		{
			return false;
		}

		/// <summary>
		/// Method invoked when a mouse event is generated for the first time.
		/// </summary>
		/// <param name="mouseEvent"></param>
		/// <returns><c>true</c>, if the event was handled, <c>false</c> otherwise.</returns>
		public virtual bool OnMouseEnter (MouseEvent mouseEvent)
		{
			return false;
		}

		/// <summary>
		/// Method invoked when a mouse event is generated for the last time.
		/// </summary>
		/// <param name="mouseEvent"></param>
		/// <returns><c>true</c>, if the event was handled, <c>false</c> otherwise.</returns>
		public virtual bool OnMouseLeave (MouseEvent mouseEvent)
		{
			return false;
		}

		/// <summary>
		/// Method invoked when a view gets focus.
		/// </summary>
		/// <returns><c>true</c>, if the event was handled, <c>false</c> otherwise.</returns>
		public virtual bool OnEnter ()
		{
			return false;
		}

		/// <summary>
		/// Method invoked when a view loses focus.
		/// </summary>
		/// <returns><c>true</c>, if the event was handled, <c>false</c> otherwise.</returns>
		public virtual bool OnLeave ()
		{
			return false;
		}
	}

	/// <summary>
	/// Determines the LayoutStyle for a view, if Absolute, during LayoutSubviews, the
	/// value from the Frame will be used, if the value is Computer, then the Frame
	/// will be updated from the X, Y Pos objects and the Width and Height Dim objects.
	/// </summary>
	public enum LayoutStyle {
		/// <summary>
		/// The position and size of the view are based on the Frame value.
		/// </summary>
		Absolute,

		/// <summary>
		/// The position and size of the view will be computed based on the
		/// X, Y, Width and Height properties and set on the Frame.
		/// </summary>
		Computed
	}

	/// <summary>
	/// View is the base class for all views on the screen and represents a visible element that can render itself and contains zero or more nested views.
	/// </summary>
	/// <remarks>
	/// <para>
	///    The View defines the base functionality for user interface elements in Terminal/gui.cs.  Views
	///    can contain one or more subviews, can respond to user input and render themselves on the screen.
	/// </para>
	/// <para>
	///    Views can either be created with an absolute position, by calling the constructor that takes a
	///    Rect parameter to specify the absolute position and size (the Frame of the View) or by setting the
	///    X, Y, Width and Height properties on the view.    Both approaches use coordinates that are relative
	///    to the container they are being added to.
	/// </para>
	/// <para>
	///    When you do not specify a Rect frame you can use the more flexible
	///    Dim and Pos objects that can dynamically update the position of a view.
	///    The X and Y properties are of type <see cref="Pos"/>
	///    and you can use either absolute positions, percentages or anchor
	///    points.   The Width and Height properties are of type
	///    <see cref="Dim"/> and can use absolute position,
	///    percentages and anchors.  These are useful as they will take
	///    care of repositioning your views if your view's frames are resized
	///    or if the terminal size changes.
	/// </para>
	/// <para>
	///    When you specify the Rect parameter to a view, you are setting the LayoutStyle to Absolute, and the
	///    view will always stay in the position that you placed it.   To change the position change the
	///    Frame property to the new position.
	/// </para>
	/// <para>
	///    Subviews can be added to a View by calling the Add method.   The container of a view is the
	///    Superview.
	/// </para>
	/// <para>
	///    Developers can call the SetNeedsDisplay method on the view to flag a region or the entire view
	///    as requiring to be redrawn.
	/// </para>
	/// <para>
	///    Views have a ColorScheme property that defines the default colors that subviews
	///    should use for rendering.   This ensures that the views fit in the context where
	///    they are being used, and allows for themes to be plugged in.   For example, the
	///    default colors for windows and toplevels uses a blue background, while it uses
	///    a white background for dialog boxes and a red background for errors.
	/// </para>
	/// <para>
	///    If a ColorScheme is not set on a view, the result of the ColorScheme is the
	///    value of the SuperView and the value might only be valid once a view has been
	///    added to a SuperView, so your subclasses should not rely on ColorScheme being
	///    set at construction time.
	/// </para>
	/// <para>
	///    Using ColorSchemes has the advantage that your application will work both
	///    in color as well as black and white displays.
	/// </para>
	/// <para>
	///    Views that are focusable should implement the PositionCursor to make sure that
	///    the cursor is placed in a location that makes sense.   Unix terminals do not have
	///    a way of hiding the cursor, so it can be distracting to have the cursor left at
	///    the last focused view.   So views should make sure that they place the cursor
	///    in a visually sensible place.
	/// </para>
	/// <para>
	///    The metnod LayoutSubviews is invoked when the size or layout of a view has
	///    changed.   The default processing system will keep the size and dimensions
	///    for views that use the LayoutKind.Absolute, and will recompute the
	///    frames for the vies that use LayoutKind.Computed.
	/// </para>
	/// </remarks>
	public class View : Responder, IEnumerable {
		internal enum Direction {
			Forward,
			Backward
		}

		View container = null;
		View focused = null;
		Direction focusDirection;

		/// <summary>
		/// Event fired when the view get focus.
		/// </summary>
		public event EventHandler Enter;

		/// <summary>
		/// Event fired when the view lost focus.
		/// </summary>
		public event EventHandler Leave;

		/// <summary>
		/// Event fired when the view receives the mouse event for the first time.
		/// </summary>
		public event EventHandler<MouseEvent> MouseEnter;

		/// <summary>
		/// Event fired when the view loses mouse event for the last time.
		/// </summary>
		public event EventHandler<MouseEvent> MouseLeave;

		internal Direction FocusDirection {
			get => SuperView?.FocusDirection ?? focusDirection;
			set {
				if (SuperView != null)
					SuperView.FocusDirection = value;
				else
					focusDirection = value;
			}
		}

		/// <summary>
		/// Points to the current driver in use by the view, it is a convenience property
		/// for simplifying the development of new views.
		/// </summary>
		public static ConsoleDriver Driver { get { return Application.Driver; } }

		static IList<View> empty = new List<View> (0).AsReadOnly ();

		// This is null, and allocated on demand.
		List<View> subviews;

		/// <summary>
		/// This returns a list of the subviews contained by this view.
		/// </summary>
		/// <value>The subviews.</value>
		public IList<View> Subviews => subviews == null ? empty : subviews.AsReadOnly ();

		// Internally, we use InternalSubviews rather than subviews, as we do not expect us
		// to make the same mistakes our users make when they poke at the Subviews.
		internal IList<View> InternalSubviews => subviews ?? empty;

		internal Rect NeedDisplay { get; private set; } = Rect.Empty;

		// The frame for the object
		Rect frame;

		/// <summary>
		/// Gets or sets an identifier for the view;
		/// </summary>
		/// <value>The identifier.</value>
		public ustring Id { get; set; } = "";

		/// <summary>
		/// Returns a value indicating if this View is currently on Top (Active)
		/// </summary>
		public bool IsCurrentTop {
			get {
				return Application.Current == this;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="View"/> want mouse position reports.
		/// </summary>
		/// <value><c>true</c> if want mouse position reports; otherwise, <c>false</c>.</value>
		public virtual bool WantMousePositionReports { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="View"/> want continuous button pressed event.
		/// </summary>
		public virtual bool WantContinuousButtonPressed { get; set; } = false;
		/// <summary>
		/// Gets or sets the frame for the view.
		/// </summary>
		/// <value>The frame.</value>
		/// <remarks>
		///    Altering the Frame of a view will trigger the redrawing of the
		///    view as well as the redrawing of the affected regions in the superview.
		/// </remarks>
		public virtual Rect Frame {
			get => frame;
			set {
				if (SuperView != null) {
					SuperView.SetNeedsDisplay (frame);
					SuperView.SetNeedsDisplay (value);
				}
				frame = value;

				SetNeedsLayout ();
				SetNeedsDisplay (frame);
			}
		}

		/// <summary>
		/// Gets an enumerator that enumerates the subviews in this view.
		/// </summary>
		/// <returns>The enumerator.</returns>
		public IEnumerator GetEnumerator ()
		{
			foreach (var v in InternalSubviews)
				yield return v;
		}

		LayoutStyle layoutStyle;

		/// <summary>
		/// Controls how the view's Frame is computed during the LayoutSubviews method, if Absolute, then
		/// LayoutSubviews does not change the Frame properties, otherwise the Frame is updated from the
		/// values in X, Y, Width and Height properties.
		/// </summary>
		/// <value>The layout style.</value>
		public LayoutStyle LayoutStyle {
			get => layoutStyle;
			set {
				layoutStyle = value;
				SetNeedsLayout ();
			}
		}

		/// <summary>
		/// The bounds represent the View-relative rectangle used for this view.   Updates to the Bounds update the Frame, and has the same side effects as updating the frame.
		/// </summary>
		/// <value>The bounds.</value>
		public Rect Bounds {
			get => new Rect (Point.Empty, Frame.Size);
			set {
				Frame = new Rect (frame.Location, value.Size);
			}
		}

		Pos x, y;
		/// <summary>
		/// Gets or sets the X position for the view (the column).  This is only used when the LayoutStyle is Computed, if the
		/// LayoutStyle is set to Absolute, this value is ignored.
		/// </summary>
		/// <value>The X Position.</value>
		public Pos X {
			get => x;
			set {
				x = value;
				SetNeedsLayout ();
			}
		}

		/// <summary>
		/// Gets or sets the Y position for the view (line).  This is only used when the LayoutStyle is Computed, if the
		/// LayoutStyle is set to Absolute, this value is ignored.
		/// </summary>
		/// <value>The y position (line).</value>
		public Pos Y {
			get => y;
			set {
				y = value;
				SetNeedsLayout ();
			}
		}

		Dim width, height;

		/// <summary>
		/// Gets or sets the width for the view. This is only used when the LayoutStyle is Computed, if the
		/// LayoutStyle is set to Absolute, this value is ignored.
		/// </summary>
		/// <value>The width.</value>
		public Dim Width {
			get => width;
			set {
				width = value;
				SetNeedsLayout ();
			}
		}

		/// <summary>
		/// Gets or sets the height for the view. This is only used when the LayoutStyle is Computed, if the
		/// LayoutStyle is set to Absolute, this value is ignored.
		/// </summary>
		/// <value>The height.</value>
		public Dim Height {
			get => height;
			set {
				height = value;
				SetNeedsLayout ();
			}
		}

		/// <summary>
		/// Returns the container for this view, or null if this view has not been added to a container.
		/// </summary>
		/// <value>The super view.</value>
		public View SuperView => container;

		/// <summary>
		/// Initializes a new instance of the <see cref="View"/> class with the absolute
		/// dimensions specified in the frame.   If you want to have Views that can be positioned with
		/// Pos and Dim properties on X, Y, Width and Height, use the empty constructor.
		/// </summary>
		/// <param name="frame">The region covered by this view.</param>
		public View (Rect frame)
		{
			this.Frame = frame;
			CanFocus = false;
			LayoutStyle = LayoutStyle.Absolute;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="View"/> class and sets the
		/// view up for Computed layout, which will use the values in X, Y, Width and Height to
		/// compute the View's Frame.
		/// </summary>
		public View ()
		{
			CanFocus = false;
			LayoutStyle = LayoutStyle.Computed;
		}

		/// <summary>
		/// Invoke to flag that this view needs to be redisplayed, by any code
		/// that alters the state of the view.
		/// </summary>
		public void SetNeedsDisplay ()
		{
			SetNeedsDisplay (Bounds);
		}

		internal bool layoutNeeded = true;

		internal void SetNeedsLayout ()
		{
			if (layoutNeeded)
				return;
			layoutNeeded = true;
			if (SuperView == null)
				return;
			SuperView.SetNeedsLayout ();
		}

		/// <summary>
		/// Flags the specified rectangle region on this view as needing to be repainted.
		/// </summary>
		/// <param name="region">The region that must be flagged for repaint.</param>
		public void SetNeedsDisplay (Rect region)
		{
			if (NeedDisplay == null || NeedDisplay.IsEmpty)
				NeedDisplay = region;
			else {
				var x = Math.Min (NeedDisplay.X, region.X);
				var y = Math.Min (NeedDisplay.Y, region.Y);
				var w = Math.Max (NeedDisplay.Width, region.Width);
				var h = Math.Max (NeedDisplay.Height, region.Height);
				NeedDisplay = new Rect (x, y, w, h);
			}
			if (container != null)
				container.ChildNeedsDisplay ();
			if (subviews == null)
				return;
			foreach (var view in subviews)
				if (view.Frame.IntersectsWith (region)) {
					var childRegion = Rect.Intersect (view.Frame, region);
					childRegion.X -= view.Frame.X;
					childRegion.Y -= view.Frame.Y;
					view.SetNeedsDisplay (childRegion);
				}
		}

		internal bool childNeedsDisplay;

		/// <summary>
		/// Flags this view for requiring the children views to be repainted.
		/// </summary>
		public void ChildNeedsDisplay ()
		{
			childNeedsDisplay = true;
			if (container != null)
				container.ChildNeedsDisplay ();
		}

		/// <summary>
		///   Adds a subview to this view.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public virtual void Add (View view)
		{
			if (view == null)
				return;
			if (subviews == null)
				subviews = new List<View> ();
			subviews.Add (view);
			view.container = this;
			if (view.CanFocus)
				CanFocus = true;
			SetNeedsLayout ();
			SetNeedsDisplay ();
		}

		/// <summary>
		/// Adds the specified views to the view.
		/// </summary>
		/// <param name="views">Array of one or more views (can be optional parameter).</param>
		public void Add (params View [] views)
		{
			if (views == null)
				return;
			foreach (var view in views)
				Add (view);
		}

		/// <summary>
		///   Removes all the widgets from this container.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public virtual void RemoveAll ()
		{
			if (subviews == null)
				return;

			while (subviews.Count > 0) {
				Remove (subviews [0]);
			}
		}

		/// <summary>
		///   Removes a widget from this container.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public virtual void Remove (View view)
		{
			if (view == null || subviews == null)
				return;

			SetNeedsLayout ();
			SetNeedsDisplay ();
			var touched = view.Frame;
			subviews.Remove (view);
			view.container = null;

			if (subviews.Count < 1)
				this.CanFocus = false;

			foreach (var v in subviews) {
				if (v.Frame.IntersectsWith (touched))
					view.SetNeedsDisplay ();
			}
		}

		void PerformActionForSubview (View subview, Action<View> action)
		{
			if (subviews.Contains (subview)) {
				action (subview);
			}

			SetNeedsDisplay ();
			subview.SetNeedsDisplay ();
		}

		/// <summary>
		/// Brings the specified subview to the front so it is drawn on top of any other views.
		/// </summary>
		/// <param name="subview">The subview to send to the front</param>
		/// <remarks>
		///   <seealso cref="SendSubviewToBack"/>.
		/// </remarks>
		public void BringSubviewToFront (View subview)
		{
			PerformActionForSubview (subview, x => {
				subviews.Remove (x);
				subviews.Add (x);
			});
		}

		/// <summary>
		/// Sends the specified subview to the front so it is the first view drawn
		/// </summary>
		/// <param name="subview">The subview to send to the front</param>
		/// <remarks>
		///   <seealso cref="BringSubviewToFront(View)"/>.
		/// </remarks>
		public void SendSubviewToBack (View subview)
		{
			PerformActionForSubview (subview, x => {
				subviews.Remove (x);
				subviews.Insert (0, subview);
			});
		}

		/// <summary>
		/// Moves the subview backwards in the hierarchy, only one step
		/// </summary>
		/// <param name="subview">The subview to send backwards</param>
		/// <remarks>
		/// If you want to send the view all the way to the back use SendSubviewToBack.
		/// </remarks>
		public void SendSubviewBackwards (View subview)
		{
			PerformActionForSubview (subview, x => {
				var idx = subviews.IndexOf (x);
				if (idx > 0) {
					subviews.Remove (x);
					subviews.Insert (idx - 1, x);
				}
			});
		}

		/// <summary>
		/// Moves the subview backwards in the hierarchy, only one step
		/// </summary>
		/// <param name="subview">The subview to send backwards</param>
		/// <remarks>
		/// If you want to send the view all the way to the back use SendSubviewToBack.
		/// </remarks>
		public void BringSubviewForward (View subview)
		{
			PerformActionForSubview (subview, x => {
				var idx = subviews.IndexOf (x);
				if (idx + 1 < subviews.Count) {
					subviews.Remove (x);
					subviews.Insert (idx + 1, x);
				}
			});
		}

		/// <summary>
		///   Clears the view region with the current color.
		/// </summary>
		/// <remarks>
		///   <para>
		///     This clears the entire region used by this view.
		///   </para>
		/// </remarks>
		public void Clear ()
		{
			var h = Frame.Height;
			var w = Frame.Width;
			for (int line = 0; line < h; line++) {
				Move (0, line);
				for (int col = 0; col < w; col++)
					Driver.AddRune (' ');
			}
		}

		/// <summary>
		///   Clears the specified rectangular region with the current color
		/// </summary>
		public void Clear (Rect r)
		{
			var h = r.Height;
			var w = r.Width;
			for (int line = r.Y; line < r.Y + h; line++) {
				Driver.Move (r.X, line);
				for (int col = 0; col < w; col++)
					Driver.AddRune (' ');
			}
		}

		/// <summary>
		/// Converts the (col,row) position from the view into a screen (col,row).  The values are clamped to (0..ScreenDim-1)
		/// </summary>
		/// <param name="col">View-based column.</param>
		/// <param name="row">View-based row.</param>
		/// <param name="rcol">Absolute column, display relative.</param>
		/// <param name="rrow">Absolute row, display relative.</param>
		/// <param name="clipped">Whether to clip the result of the ViewToScreen method, if set to true, the rcol, rrow values are clamped to the screen dimensions.</param>
		internal void ViewToScreen (int col, int row, out int rcol, out int rrow, bool clipped = true)
		{
			// Computes the real row, col relative to the screen.
			rrow = row + frame.Y;
			rcol = col + frame.X;
			var ccontainer = container;
			while (ccontainer != null) {
				rrow += ccontainer.frame.Y;
				rcol += ccontainer.frame.X;
				ccontainer = ccontainer.container;
			}

			// The following ensures that the cursor is always in the screen boundaries.
			if (clipped) {
				rrow = Math.Max (0, Math.Min (rrow, Driver.Rows - 1));
				rcol = Math.Max (0, Math.Min (rcol, Driver.Cols - 1));
			}
		}

		/// <summary>
		/// Converts a point from screen coordinates into the view coordinate space.
		/// </summary>
		/// <returns>The mapped point.</returns>
		/// <param name="x">X screen-coordinate point.</param>
		/// <param name="y">Y screen-coordinate point.</param>
		public Point ScreenToView (int x, int y)
		{
			if (SuperView == null) {
				return new Point (x - Frame.X, y - frame.Y);
			} else {
				var parent = SuperView.ScreenToView (x, y);
				return new Point (parent.X - frame.X, parent.Y - frame.Y);
			}
		}

		// Converts a rectangle in view coordinates to screen coordinates.
		Rect RectToScreen (Rect rect)
		{
			ViewToScreen (rect.X, rect.Y, out var x, out var y, clipped: false);
			return new Rect (x, y, rect.Width, rect.Height);
		}

		// Clips a rectangle in screen coordinates to the dimensions currently available on the screen
		Rect ScreenClip (Rect rect)
		{
			var x = rect.X < 0 ? 0 : rect.X;
			var y = rect.Y < 0 ? 0 : rect.Y;
			var w = rect.X + rect.Width >= Driver.Cols ? Driver.Cols - rect.X : rect.Width;
			var h = rect.Y + rect.Height >= Driver.Rows ? Driver.Rows - rect.Y : rect.Height;

			return new Rect (x, y, w, h);
		}

		/// <summary>
		/// Sets the Console driver's clip region to the current View's Bounds.
		/// </summary>
		/// <returns>The existing driver's Clip region, which can be then set by setting the Driver.Clip property.</returns>
		public Rect ClipToBounds ()
		{
			return SetClip (Bounds);
		}

		/// <summary>
		/// Sets the clipping region to the specified region, the region is view-relative
		/// </summary>
		/// <returns>The previous clip region.</returns>
		/// <param name="rect">Rectangle region to clip into, the region is view-relative.</param>
		public Rect SetClip (Rect rect)
		{
			var bscreen = RectToScreen (rect);
			var previous = Driver.Clip;
			Driver.Clip = ScreenClip (RectToScreen (Bounds));
			return previous;
		}

		/// <summary>
		/// Draws a frame in the current view, clipped by the boundary of this view
		/// </summary>
		/// <param name="rect">Rectangular region for the frame to be drawn.</param>
		/// <param name="padding">The padding to add to the drawn frame.</param>
		/// <param name="fill">If set to <c>true</c> it fill will the contents.</param>
		public void DrawFrame (Rect rect, int padding = 0, bool fill = false)
		{
			var scrRect = RectToScreen (rect);
			var savedClip = Driver.Clip;
			Driver.Clip = ScreenClip (RectToScreen (Bounds));
			Driver.DrawFrame (scrRect, padding, fill);
			Driver.Clip = savedClip;
		}

		/// <summary>
		/// Utility function to draw strings that contain a hotkey
		/// </summary>
		/// <param name="text">String to display, the underscoore before a letter flags the next letter as the hotkey.</param>
		/// <param name="hotColor">Hot color.</param>
		/// <param name="normalColor">Normal color.</param>
		public void DrawHotString (ustring text, Attribute hotColor, Attribute normalColor)
		{
			Driver.SetAttribute (normalColor);
			foreach (var rune in text) {
				if (rune == '_') {
					Driver.SetAttribute (hotColor);
					continue;
				}
				Driver.AddRune (rune);
				Driver.SetAttribute (normalColor);
			}
		}

		/// <summary>
		/// Utility function to draw strings that contains a hotkey using a colorscheme and the "focused" state.
		/// </summary>
		/// <param name="text">String to display, the underscoore before a letter flags the next letter as the hotkey.</param>
		/// <param name="focused">If set to <c>true</c> this uses the focused colors from the color scheme, otherwise the regular ones.</param>
		/// <param name="scheme">The color scheme to use.</param>
		public void DrawHotString (ustring text, bool focused, ColorScheme scheme)
		{
			if (focused)
				DrawHotString (text, scheme.HotFocus, scheme.Focus);
			else
				DrawHotString (text, scheme.HotNormal, scheme.Normal);
		}

		/// <summary>
		/// This moves the cursor to the specified column and row in the view.
		/// </summary>
		/// <returns>The move.</returns>
		/// <param name="col">Col.</param>
		/// <param name="row">Row.</param>
		public void Move (int col, int row)
		{
			ViewToScreen (col, row, out var rcol, out var rrow);
			Driver.Move (rcol, rrow);
		}

		/// <summary>
		///   Positions the cursor in the right position based on the currently focused view in the chain.
		/// </summary>
		public virtual void PositionCursor ()
		{
			if (focused != null)
				focused.PositionCursor ();
			else
				Move (frame.X, frame.Y);
		}

		/// <inheritdoc cref="HasFocus"/>
		public override bool HasFocus {
			get {
				return base.HasFocus;
			}
			internal set {
				if (base.HasFocus != value)
					if (value)
						OnEnter ();
					else
						OnLeave ();
				SetNeedsDisplay ();
				base.HasFocus = value;

				// Remove focus down the chain of subviews if focus is removed
				if (!value && focused != null) {
					focused.OnLeave ();
					focused.HasFocus = false;
					focused = null;
				}
			}
		}

		/// <inheritdoc cref="OnEnter"/>
		public override bool OnEnter ()
		{
			Enter?.Invoke (this, new EventArgs ());
			return base.OnEnter ();
		}

		/// <inheritdoc cref="OnLeave"/>
		public override bool OnLeave ()
		{
			Leave?.Invoke (this, new EventArgs ());
			return base.OnLeave ();
		}

		/// <summary>
		/// Returns the currently focused view inside this view, or null if nothing is focused.
		/// </summary>
		/// <value>The focused.</value>
		public View Focused => focused;

		/// <summary>
		/// Returns the most focused view in the chain of subviews (the leaf view that has the focus).
		/// </summary>
		/// <value>The most focused.</value>
		public View MostFocused {
			get {
				if (Focused == null)
					return null;
				var most = Focused.MostFocused;
				if (most != null)
					return most;
				return Focused;
			}
		}

		/// <summary>
		/// The color scheme for this view, if it is not defined, it returns the parent's
		/// color scheme.
		/// </summary>
		public ColorScheme ColorScheme {
			get {
				if (colorScheme == null)
					return SuperView?.ColorScheme;
				return colorScheme;
			}
			set {
				colorScheme = value;
			}
		}

		ColorScheme colorScheme;

		/// <summary>
		/// Displays the specified character in the specified column and row.
		/// </summary>
		/// <param name="col">Col.</param>
		/// <param name="row">Row.</param>
		/// <param name="ch">Ch.</param>
		public void AddRune (int col, int row, Rune ch)
		{
			if (row < 0 || col < 0)
				return;
			if (row > frame.Height - 1 || col > frame.Width - 1)
				return;
			Move (col, row);
			Driver.AddRune (ch);
		}

		/// <summary>
		/// Removes the SetNeedsDisplay and the ChildNeedsDisplay setting on this view.
		/// </summary>
		protected void ClearNeedsDisplay ()
		{
			NeedDisplay = Rect.Empty;
			childNeedsDisplay = false;
		}

		/// <summary>
		/// Performs a redraw of this view and its subviews, only redraws the views that have been flagged for a re-display.
		/// </summary>
		/// <param name="region">The region to redraw, this is relative to the view itself.</param>
		/// <remarks>
		/// <para>
		///    Views should set the color that they want to use on entry, as otherwise this will inherit
		///    the last color that was set globaly on the driver.
		/// </para>
		/// </remarks>
		public virtual void Redraw (Rect region)
		{
			var clipRect = new Rect (Point.Empty, frame.Size);

			if (subviews != null) {
				foreach (var view in subviews) {
					if (view.NeedDisplay != null && (!view.NeedDisplay.IsEmpty || view.childNeedsDisplay)) {
						if (view.Frame.IntersectsWith (clipRect) && view.Frame.IntersectsWith (region)) {

							// FIXED: optimize this by computing the intersection of region and view.Bounds
							if (view.layoutNeeded)
								view.LayoutSubviews ();
							Application.CurrentView = view;
							view.Redraw (view.Bounds);
						}
						view.NeedDisplay = Rect.Empty;
						view.childNeedsDisplay = false;
					}
				}
			}
			ClearNeedsDisplay ();
		}

		/// <summary>
		/// Focuses the specified sub-view.
		/// </summary>
		/// <param name="view">View.</param>
		public void SetFocus (View view)
		{
			if (view == null)
				return;
			//Console.WriteLine ($"Request to focus {view}");
			if (!view.CanFocus)
				return;
			if (focused == view)
				return;

			// Make sure that this view is a subview
			View c;
			for (c = view.container; c != null; c = c.container)
				if (c == this)
					break;
			if (c == null)
				throw new ArgumentException ("the specified view is not part of the hierarchy of this view");

			if (focused != null)
				focused.HasFocus = false;

			focused = view;
			focused.HasFocus = true;
			focused.EnsureFocus ();

			// Send focus upwards
			SuperView?.SetFocus (this);
		}

		/// <summary>
		/// Specifies the event arguments for <see cref="KeyEvent"/>
		/// </summary>
		public class KeyEventEventArgs : EventArgs {
			/// <summary>
			/// Constructs.
			/// </summary>
			/// <param name="ke"></param>
			public KeyEventEventArgs(KeyEvent ke) => KeyEvent = ke;
			/// <summary>
			/// The <see cref="KeyEvent"/> for the event.
			/// </summary>
			public KeyEvent KeyEvent { get; set; }
			/// <summary>
			/// Indicates if the current Key event has already been processed and the driver should stop notifying any other event subscriber.
			/// Its important to set this value to true specially when updating any View's layout from inside the subscriber method.
			/// </summary>
			public bool Handled { get; set; } = false;
		}

		/// <summary>
		/// Invoked when a character key is pressed and occurs after the key up event.
		/// </summary>
		public event EventHandler<KeyEventEventArgs> KeyPress;

		/// <inheritdoc cref="ProcessKey"/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{

			KeyEventEventArgs args = new KeyEventEventArgs (keyEvent);
			KeyPress?.Invoke (this, args);
			if (args.Handled)
				return true;
			if (Focused?.ProcessKey (keyEvent) == true)
				return true;

			return false;
		}

		/// <inheritdoc cref="ProcessHotKey"/>
		public override bool ProcessHotKey (KeyEvent keyEvent)
		{
			KeyEventEventArgs args = new KeyEventEventArgs (keyEvent);
			KeyPress?.Invoke (this, args);
			if (args.Handled)
				return true;
			if (subviews == null || subviews.Count == 0)
				return false;
			foreach (var view in subviews)
				if (view.ProcessHotKey (keyEvent))
					return true;
			return false;
		}

		/// <inheritdoc cref="ProcessColdKey"/>
		public override bool ProcessColdKey (KeyEvent keyEvent)
		{
			KeyEventEventArgs args = new KeyEventEventArgs (keyEvent);
			KeyPress?.Invoke (this, args);
			if (args.Handled)
				return true;
			if (subviews == null || subviews.Count == 0)
				return false;
			foreach (var view in subviews)
				if (view.ProcessColdKey (keyEvent))
					return true;
			return false;
		}

		/// <summary>
		/// Invoked when a key is pressed
		/// </summary>
		public event EventHandler<KeyEventEventArgs> KeyDown;

		/// <param name="keyEvent">Contains the details about the key that produced the event.</param>
		public override bool OnKeyDown (KeyEvent keyEvent)
		{
			KeyEventEventArgs args = new KeyEventEventArgs (keyEvent);
			KeyDown?.Invoke (this, args);
			if (args.Handled)
				return true;
			if (subviews == null || subviews.Count == 0)
				return false;
			foreach (var view in subviews)
				if (view.OnKeyDown (keyEvent))
					return true;

			return false;
		}

		/// <summary>
		/// Invoked when a key is released
		/// </summary>
		public event EventHandler<KeyEventEventArgs> KeyUp;

		/// <param name="keyEvent">Contains the details about the key that produced the event.</param>
		public override bool OnKeyUp (KeyEvent keyEvent)
		{
			KeyEventEventArgs args = new KeyEventEventArgs (keyEvent);
			KeyUp?.Invoke (this, args);
			if (args.Handled)
				return true;
			if (subviews == null || subviews.Count == 0)
				return false;
			foreach (var view in subviews)
				if (view.OnKeyUp (keyEvent))
					return true;

			return false;
		}

		/// <summary>
		/// Finds the first view in the hierarchy that wants to get the focus if nothing is currently focused, otherwise, it does nothing.
		/// </summary>
		public void EnsureFocus ()
		{
			if (focused == null)
				if (FocusDirection == Direction.Forward)
					FocusFirst ();
				else
					FocusLast ();
		}

		/// <summary>
		/// Focuses the first focusable subview if one exists.
		/// </summary>
		public void FocusFirst ()
		{
			if (subviews == null) {
				SuperView?.SetFocus (this);
				return;
			}

			foreach (var view in subviews) {
				if (view.CanFocus) {
					SetFocus (view);
					return;
				}
			}
		}

		/// <summary>
		/// Focuses the last focusable subview if one exists.
		/// </summary>
		public void FocusLast ()
		{
			if (subviews == null) {
				SuperView?.SetFocus (this);
				return;
			}

			for (int i = subviews.Count; i > 0;) {
				i--;

				View v = subviews [i];
				if (v.CanFocus) {
					SetFocus (v);
					return;
				}
			}
		}

		/// <summary>
		/// Focuses the previous view.
		/// </summary>
		/// <returns><c>true</c>, if previous was focused, <c>false</c> otherwise.</returns>
		public bool FocusPrev ()
		{
			FocusDirection = Direction.Backward;
			if (subviews == null || subviews.Count == 0)
				return false;

			if (focused == null) {
				FocusLast ();
				return focused != null;
			}
			int focused_idx = -1;
			for (int i = subviews.Count; i > 0;) {
				i--;
				View w = subviews [i];

				if (w.HasFocus) {
					if (w.FocusPrev ())
						return true;
					focused_idx = i;
					continue;
				}
				if (w.CanFocus && focused_idx != -1) {
					focused.HasFocus = false;

					if (w != null && w.CanFocus)
						w.FocusLast ();

					SetFocus (w);
					return true;
				}
			}
			if (focused != null) {
				focused.HasFocus = false;
				focused = null;
			}
			return false;
		}

		/// <summary>
		/// Focuses the next view.
		/// </summary>
		/// <returns><c>true</c>, if next was focused, <c>false</c> otherwise.</returns>
		public bool FocusNext ()
		{
			FocusDirection = Direction.Forward;
			if (subviews == null || subviews.Count == 0)
				return false;

			if (focused == null) {
				FocusFirst ();
				return focused != null;
			}
			int n = subviews.Count;
			int focused_idx = -1;
			for (int i = 0; i < n; i++) {
				View w = subviews [i];

				if (w.HasFocus) {
					if (w.FocusNext ())
						return true;
					focused_idx = i;
					continue;
				}
				if (w.CanFocus && focused_idx != -1) {
					focused.HasFocus = false;

					if (w != null && w.CanFocus)
						w.FocusFirst ();

					SetFocus (w);
					return true;
				}
			}
			if (focused != null) {
				focused.HasFocus = false;
				focused = null;
			}
			return false;
		}

		/// <summary>
		/// Computes the RelativeLayout for the view, given the frame for its container.
		/// </summary>
		/// <param name="hostFrame">The Frame for the host.</param>
		internal void RelativeLayout (Rect hostFrame)
		{
			int w, h, _x, _y;

			if (x is Pos.PosCenter) {
				if (width == null)
					w = hostFrame.Width;
				else
					w = width.Anchor (hostFrame.Width);
				_x = x.Anchor (hostFrame.Width - w);
			} else {
				if (x == null)
					_x = 0;
				else
					_x = x.Anchor (hostFrame.Width);
				if (width == null)
					w = hostFrame.Width;
				else
					w = width.Anchor (hostFrame.Width - _x);
			}

			if (y is Pos.PosCenter) {
				if (height == null)
					h = hostFrame.Height;
				else
					h = height.Anchor (hostFrame.Height);
				_y = y.Anchor (hostFrame.Height - h);
			} else {
				if (y == null)
					_y = 0;
				else
					_y = y.Anchor (hostFrame.Height);
				if (height == null)
					h = hostFrame.Height;
				else
					h = height.Anchor (hostFrame.Height - _y);
			}
			Frame = new Rect (_x, _y, w, h);
			// layoutNeeded = false;
		}

		// https://en.wikipedia.org/wiki/Topological_sorting
		List<View> TopologicalSort (HashSet<View> nodes, HashSet<(View From, View To)> edges)
		{
			var result = new List<View> ();

			// Set of all nodes with no incoming edges
			var S = new HashSet<View> (nodes.Where (n => edges.All (e => e.To.Equals (n) == false)));

			while (S.Any ()) {
				//  remove a node n from S
				var n = S.First ();
				S.Remove (n);

				// add n to tail of L
				if (n != this?.SuperView)
					result.Add (n);

				// for each node m with an edge e from n to m do
				foreach (var e in edges.Where (e => e.From.Equals (n)).ToArray ()) {
					var m = e.To;

					// remove edge e from the graph
					edges.Remove (e);

					// if m has no other incoming edges then
					if (edges.All (me => me.To.Equals (m) == false) && m != this?.SuperView) {
						// insert m into S
						S.Add (m);
					}
				}
			}

			// if graph has edges then
			if (edges.Any ()) {
				// return error (graph has at least one cycle)
				return null;
			} else {
				// return L (a topologically sorted order)
				return result;
			}
		}

		/// <summary>
		/// This virtual method is invoked when a view starts executing or
		/// when the dimensions of the view have changed, for example in
		/// response to the container view or terminal resizing.
		/// </summary>
		public virtual void LayoutSubviews ()
		{
			if (!layoutNeeded)
				return;

			// Sort out the dependencies of the X, Y, Width, Height properties
			var nodes = new HashSet<View> ();
			var edges = new HashSet<(View, View)> ();

			foreach (var v in InternalSubviews) {
				nodes.Add (v);
				if (v.LayoutStyle == LayoutStyle.Computed) {
					if (v.X is Pos.PosView vX)
						edges.Add ((vX.Target, v));
					if (v.Y is Pos.PosView vY)
						edges.Add ((vY.Target, v));
					if (v.Width is Dim.DimView vWidth)
						edges.Add ((vWidth.Target, v));
					if (v.Height is Dim.DimView vHeight)
						edges.Add ((vHeight.Target, v));
				}
			}

			var ordered = TopologicalSort (nodes, edges);
			if (ordered == null)
				throw new Exception ("There is a recursive cycle in the relative Pos/Dim in the views of " + this);

			foreach (var v in ordered) {
				if (v.LayoutStyle == LayoutStyle.Computed)
					v.RelativeLayout (Frame);

				v.LayoutSubviews ();
				v.layoutNeeded = false;

			}

			if (SuperView == Application.Top && layoutNeeded && ordered.Count == 0 && LayoutStyle == LayoutStyle.Computed) {
				RelativeLayout (Frame);
			}

			layoutNeeded = false;
		}

		/// <inheritdoc cref="ToString"/>
		public override string ToString ()
		{
			return $"{GetType ().Name}({Id})({Frame})";
		}

		/// <inheritdoc cref="OnMouseEnter(Gui.MouseEvent)"/>
		public override bool OnMouseEnter (MouseEvent mouseEvent)
		{
			if (!base.OnMouseEnter (mouseEvent)) {
				MouseEnter?.Invoke (this, mouseEvent);
				return false;
			}
			return true;
		}

		/// <inheritdoc cref="OnMouseLeave(Gui.MouseEvent)"/>
		public override bool OnMouseLeave (MouseEvent mouseEvent)
		{
			if (!base.OnMouseLeave (mouseEvent)) {
				MouseLeave?.Invoke (this, mouseEvent);
				return false;
			}
			return true;
		}
	}

	/// <summary>
	/// Toplevel views can be modally executed.
	/// </summary>
	/// <remarks>
	///   <para>
	///     Toplevels can be modally executing views, and they return control
	///     to the caller when the "Running" property is set to false, or
	///     by calling <see cref="M:Terminal.Gui.Application.RequestStop()"/>
	///   </para>
	///   <para>
	///     There will be a toplevel created for you on the first time use
	///     and can be accessed from the property <see cref="P:Terminal.Gui.Application.Top"/>,
	///     but new toplevels can be created and ran on top of it.   To run, create the
	///     toplevel and then invoke <see cref="M:Terminal.Gui.Application.Run"/> with the
	///     new toplevel.
	///   </para>
	///   <para>
	///     TopLevels can also opt-in to more sophisticated initialization
	///     by implementing <see cref="ISupportInitialize"/>. When they do
	///     so, the <see cref="ISupportInitialize.BeginInit"/> and
	///     <see cref="ISupportInitialize.EndInit"/> methods will be called
	///     before running the view.
	///     If first-run-only initialization is preferred, the <see cref="ISupportInitializeNotification"/>
	///     can be implemented too, in which case the <see cref="ISupportInitialize"/>
	///     methods will only be called if <see cref="ISupportInitializeNotification.IsInitialized"/>
	///     is <see langword="false"/>. This allows proper View inheritance hierarchies
	///     to override base class layout code optimally by doing so only on first run,
	///     instead of on every run.
	///   </para>
	/// </remarks>
	public class Toplevel : View {
		/// <summary>
		/// Gets or sets whether the Mainloop for this <see cref="Toplevel"/> is running or not. Setting
		/// this property to false will cause the MainLoop to exit. 
		/// </summary>
		public bool Running { get; set; }

		/// <summary>
		/// Fired once the Toplevel's MainLoop has started it's first iteration. 
		/// Subscribe to this event to perform tasks when the <see cref="Toplevel"/> has been laid out and focus has been set.
		/// changes. A Ready event handler is a good place to finalize initialization after calling `<see cref="Application.Run()"/>(topLevel)`. 
		/// </summary>
		public event EventHandler Ready;

		/// <summary>
		/// Called from Application.RunLoop after the <see cref="Toplevel"/> has entered it's first iteration of the loop. 
		/// </summary>
		internal virtual void OnReady ()
		{
			Ready?.Invoke (this, EventArgs.Empty);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Toplevel"/> class with the specified absolute layout.
		/// </summary>
		/// <param name="frame">Frame.</param>
		public Toplevel (Rect frame) : base (frame)
		{
			Initialize ();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Toplevel"/> class with Computed layout, defaulting to <see langword="async"/> full screen.
		/// </summary>
		public Toplevel () : base ()
		{
			Initialize ();
			Width = Dim.Fill ();
			Height = Dim.Fill ();
		}

		void Initialize ()
		{
			ColorScheme = Colors.Base;
		}

		/// <summary>
		/// Convenience factory method that creates a new toplevel with the current terminal dimensions.
		/// </summary>
		/// <returns>The create.</returns>
		public static Toplevel Create ()
		{
			return new Toplevel (new Rect (0, 0, Driver.Cols, Driver.Rows));
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Toplevel"/> can focus.
		/// </summary>
		/// <value><c>true</c> if can focus; otherwise, <c>false</c>.</value>
		public override bool CanFocus {
			get => true;
		}

		/// <summary>
		/// Determines whether the <see cref="Toplevel"/> is modal or not.
		/// Causes <see cref="ProcessKey(KeyEvent)"/> to propagate keys upwards
		/// by default unless set to <see langword="true"/>.
		/// </summary>
		public bool Modal { get; set; }

		/// <summary>
		/// Check id current toplevel has menu bar
		/// </summary>
		public MenuBar MenuBar { get; set; }

		/// <summary>
		/// Check id current toplevel has status bar
		/// </summary>
		public StatusBar StatusBar { get; set; }

		///<inheritdoc cref="ProcessKey"/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{
			if (base.ProcessKey (keyEvent))
				return true;

			switch (keyEvent.Key) {
			case Key.ControlQ:
				// FIXED: stop current execution of this container
				Application.RequestStop ();
				break;
			case Key.ControlZ:
				Driver.Suspend ();
				return true;

#if false
			case Key.F5:
				Application.DebugDrawBounds = !Application.DebugDrawBounds;
				SetNeedsDisplay ();
				return true;
#endif
			case Key.Tab:
			case Key.CursorRight:
			case Key.CursorDown:
			case Key.ControlI: // Unix
				var old = Focused;
				if (!FocusNext ())
					FocusNext ();
				if (old != Focused) {
					old?.SetNeedsDisplay ();
					Focused?.SetNeedsDisplay ();
				}
				return true;
			case Key.CursorLeft:
			case Key.CursorUp:
			case Key.BackTab:
				old = Focused;
				if (!FocusPrev ())
					FocusPrev ();
				if (old != Focused) {
					old?.SetNeedsDisplay ();
					Focused?.SetNeedsDisplay ();
				}
				return true;

			case Key.ControlL:
				Application.Refresh ();
				return true;
			}
			return false;
		}

		///<inheritdoc cref="Add"/>
		public override void Add (View view)
		{
			if (this == Application.Top) {
				if (view is MenuBar)
					MenuBar = view as MenuBar;
				if (view is StatusBar)
					StatusBar = view as StatusBar;
			}
			base.Add (view);
		}

		///<inheritdoc cref="Remove"/>
		public override void Remove (View view)
		{
			if (this == Application.Top) {
				if (view is MenuBar)
					MenuBar = null;
				if (view is StatusBar)
					StatusBar = null;
			}
			base.Remove (view);
		}

		///<inheritdoc cref="RemoveAll"/>
		public override void RemoveAll ()
		{
			if (this == Application.Top) {
				MenuBar = null;
				StatusBar = null;
			}
			base.RemoveAll ();
		}

		internal void EnsureVisibleBounds (Toplevel top, int x, int y, out int nx, out int ny)
		{
			nx = Math.Max (x, 0);
			nx = nx + top.Frame.Width > Driver.Cols ? Math.Max (Driver.Cols - top.Frame.Width, 0) : nx;
			bool m, s;
			if (SuperView == null || SuperView.GetType() != typeof(Toplevel))
				m = Application.Top.MenuBar != null;
			else
				m = ((Toplevel)SuperView).MenuBar != null;
			int l = m ? 1 : 0;
			ny = Math.Max (y, l);
			if (SuperView == null || SuperView.GetType() != typeof(Toplevel))
				s = Application.Top.StatusBar != null;
			else
				s = ((Toplevel)SuperView).StatusBar != null;
			l = s ? Driver.Rows - 1 : Driver.Rows;
			ny = Math.Min (ny, l);
			ny = ny + top.Frame.Height > l ? Math.Max (l - top.Frame.Height, m ? 1 : 0) : ny;
		}

		internal void PositionToplevels ()
		{
			if (this != Application.Top) {
				EnsureVisibleBounds (this, Frame.X, Frame.Y, out int nx, out int ny);
				if ((nx != Frame.X || ny != Frame.Y) && LayoutStyle != LayoutStyle.Computed) {
					X = nx;
					Y = ny;
				}
			} else {
				foreach (var top in Subviews) {
					if (top is Toplevel) {
						EnsureVisibleBounds ((Toplevel)top, top.Frame.X, top.Frame.Y, out int nx, out int ny);
						if ((nx != top.Frame.X || ny != top.Frame.Y) && top.LayoutStyle != LayoutStyle.Computed) {
							top.X = nx;
							top.Y = ny;
						}
						if (StatusBar != null) {
							if (ny + top.Frame.Height > Driver.Rows - 1) {
								if (top.Height is Dim.DimFill)
									top.Height = Dim.Fill () - 1;
							}
							if (StatusBar.Frame.Y != Driver.Rows - 1) {
								StatusBar.Y = Driver.Rows - 1;
								SetNeedsDisplay ();
							}
						}
					}
				}
			}
		}

		///<inheritdoc cref="Redraw"/>
		public override void Redraw (Rect region)
		{
			Application.CurrentView = this;

			if (IsCurrentTop) {
				if (NeedDisplay != null && !NeedDisplay.IsEmpty) {
					Driver.SetAttribute (Colors.TopLevel.Normal);
					Clear (region);
					Driver.SetAttribute (Colors.Base.Normal);
				}
				foreach (var view in Subviews) {
					if (view.Frame.IntersectsWith (region)) {
						view.SetNeedsLayout ();
						view.SetNeedsDisplay (view.Bounds);
					}
				}

				ClearNeedsDisplay ();
			}

			base.Redraw (base.Bounds);
		}

		/// <summary>
		/// This method is invoked by Application.Begin as part of the Application.Run after
		/// the views have been laid out, and before the views are drawn for the first time.
		/// </summary>
		public virtual void WillPresent ()
		{
			FocusFirst ();
		}
	}

	/// <summary>
	/// A <see cref="Toplevel"/> <see cref="View"/> that draws a frame around its region and has a "ContentView" subview where the contents are added.
	/// </summary>
	public class Window : Toplevel, IEnumerable {
		View contentView;
		ustring title;

		/// <summary>
		/// The title to be displayed for this window.
		/// </summary>
		/// <value>The title.</value>
		public ustring Title {
			get => title;
			set {
				title = value;
				SetNeedsDisplay ();
			}
		}

		class ContentView : View {
			public ContentView (Rect frame) : base (frame) { }
			public ContentView () : base () { }
#if false
			public override void Redraw (Rect region)
			{
				Driver.SetAttribute (ColorScheme.Focus);

				for (int y = 0; y < Frame.Height; y++) {
					Move (0, y);
					for (int x = 0; x < Frame.Width; x++) {

						Driver.AddRune ('x');
					}
				}
			}
#endif
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Gui.Window"/> class with an optional title and a set frame.
		/// </summary>
		/// <param name="frame">Frame.</param>
		/// <param name="title">Title.</param>
		public Window (Rect frame, ustring title = null) : this (frame, title, padding: 0)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> class with an optional title.
		/// </summary>
		/// <param name="title">Title.</param>
		public Window (ustring title = null) : this (title, padding: 0)
		{
		}

		int padding;
		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> with
		/// the specified frame for its location, with the specified border
		/// an optional title.
		/// </summary>
		/// <param name="frame">Frame.</param>
		/// <param name="padding">Number of characters to use for padding of the drawn frame.</param>
		/// <param name="title">Title.</param>
		public Window (Rect frame, ustring title = null, int padding = 0) : base (frame)
		{
			this.Title = title;
			int wb = 2 * (1 + padding);
			this.padding = padding;
			var cFrame = new Rect (1 + padding, 1 + padding, frame.Width - wb, frame.Height - wb);
			contentView = new ContentView (cFrame);
			base.Add (contentView);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> with
		/// the specified frame for its location, with the specified border
		/// an optional title.
		/// </summary>
		/// <param name="padding">Number of characters to use for padding of the drawn frame.</param>
		/// <param name="title">Title.</param>
		public Window (ustring title = null, int padding = 0) : base ()
		{
			this.Title = title;
			int wb = 1 + padding;
			this.padding = padding;
			contentView = new ContentView () {
				X = wb,
				Y = wb,
				Width = Dim.Fill (wb),
				Height = Dim.Fill (wb)
			};
			base.Add (contentView);
		}

		/// <summary>
		/// Enumerates the various <see cref="View"/>s in the embedded <see cref="ContentView"/>.
		/// </summary>
		/// <returns>The enumerator.</returns>
		public new IEnumerator GetEnumerator ()
		{
			return contentView.GetEnumerator ();
		}

		void DrawFrame (bool fill = true)
		{
			DrawFrame (new Rect (0, 0, Frame.Width, Frame.Height), padding, fill: fill);
		}

		/// <summary>
		/// Add the specified view to the <see cref="ContentView"/>.
		/// </summary>
		/// <param name="view">View to add to the window.</param>
		public override void Add (View view)
		{
			contentView.Add (view);
			if (view.CanFocus)
				CanFocus = true;
		}


		/// <summary>
		///   Removes a widget from this container.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public override void Remove (View view)
		{
			if (view == null)
				return;

			SetNeedsDisplay ();
			var touched = view.Frame;
			contentView.Remove (view);

			if (contentView.InternalSubviews.Count < 1)
				this.CanFocus = false;
		}

		/// <summary>
		///   Removes all widgets from this container.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public override void RemoveAll ()
		{
			contentView.RemoveAll ();
		}

		///<inheritdoc cref="Redraw"/>
		public override void Redraw (Rect bounds)
		{
			Application.CurrentView = this;

			if (NeedDisplay != null && !NeedDisplay.IsEmpty) {
				DrawFrameWindow ();
			}
			contentView.Redraw (contentView.Bounds);
			ClearNeedsDisplay ();
			DrawFrameWindow (false);

			void DrawFrameWindow (bool fill = true)
			{
				Driver.SetAttribute (ColorScheme.Normal);
				DrawFrame (fill);
				if (HasFocus)
					Driver.SetAttribute (ColorScheme.HotNormal);
				var width = Frame.Width - (padding + 2) * 2;
				if (Title != null && width > 4) {
					Move (1 + padding, padding);
					Driver.AddRune (' ');
					var str = Title.Length >= width ? Title [0, width - 2] : Title;
					Driver.AddStr (str);
					Driver.AddRune (' ');
				}
				Driver.SetAttribute (ColorScheme.Normal);
			}
		}

		//
		// FIXED:It does not look like the event is raised on clicked-drag
		// need to figure that out.
		//
		internal static Point? dragPosition;
		Point start;
		///<inheritdoc cref="MouseEvent(Gui.MouseEvent)"/>
		public override bool MouseEvent (MouseEvent mouseEvent)
		{
			// FIXED:The code is currently disabled, because the
			// Driver.UncookMouse does not seem to have an effect if there is
			// a pending mouse event activated.

			int nx, ny;
			if ((mouseEvent.Flags == (MouseFlags.Button1Pressed | MouseFlags.ReportMousePosition) ||
				mouseEvent.Flags == MouseFlags.Button3Pressed)) {
				if (dragPosition.HasValue) {
					if (SuperView == null) {
						Application.Top.SetNeedsDisplay (Frame);
						Application.Top.Redraw (Frame);
					} else {
						SuperView.SetNeedsDisplay (Frame);
					}
					EnsureVisibleBounds (this, mouseEvent.X + mouseEvent.OfX - start.X,
						mouseEvent.Y + mouseEvent.OfY, out nx, out ny);

					dragPosition = new Point (nx, ny);
					Frame = new Rect (nx, ny, Frame.Width, Frame.Height);
					X = nx;
					Y = ny;
					//Demo.ml2.Text = $"{dx},{dy}";

					// FIXED: optimize, only SetNeedsDisplay on the before/after regions.
					SetNeedsDisplay ();
					return true;
				} else {
					// Only start grabbing if the user clicks on the title bar.
					if (mouseEvent.Y == 0) {
						start = new Point (mouseEvent.X, mouseEvent.Y);
						dragPosition = new Point ();
						nx = mouseEvent.X - mouseEvent.OfX;
						ny = mouseEvent.Y - mouseEvent.OfY;
						dragPosition = new Point (nx, ny);
						Application.GrabMouse (this);
					}

					//Demo.ml2.Text = $"Starting at {dragPosition}";
					return true;
				}
			}

			if (mouseEvent.Flags == MouseFlags.Button1Released && dragPosition.HasValue) {
				Application.UngrabMouse ();
				Driver.UncookMouse ();
				dragPosition = null;
			}

			//Demo.ml.Text = me.ToString ();
			return false;
		}

	}

	/// <summary>
	/// The application driver for Terminal.Gui.
	/// </summary>
	/// <remarks>
	///   <para>
	///     You can hook up to the Iteration event to have your method
	///     invoked on each iteration of the mainloop.
	///   </para>
	///   <para>
	///     Creates a mainloop to process input events, handle timers and
	///     other sources of data.   It is accessible via the MainLoop property.
	///   </para>
	///   <para>
	///     When invoked sets the SynchronizationContext to one that is tied
	///     to the mainloop, allowing user code to use async/await.
	///   </para>
	/// </remarks>
	public static class Application {
		/// <summary>
		/// The current <see cref="ConsoleDriver"/> in use.
		/// </summary>
		public static ConsoleDriver Driver;

		/// <summary>
		/// The <see cref="Toplevel"/> object used for the application on startup (<seealso cref="Application.Top"/>)
		/// </summary>
		/// <value>The top.</value>
		public static Toplevel Top { get; private set; }

		/// <summary>
		/// The current <see cref="Toplevel"/> object. This is updated when <see cref="Application.Run()"/> enters and leaves to point to the current <see cref="Toplevel"/> .
		/// </summary>
		/// <value>The current.</value>
		public static Toplevel Current { get; private set; }

		/// <summary>
		/// TThe current <see cref="View"/> object being redrawn.
		/// </summary>
		/// /// <value>The current.</value>
		public static View CurrentView { get; set; }

		/// <summary>
		/// The <see cref="MainLoop"/>  driver for the applicaiton
		/// </summary>
		/// <value>The main loop.</value>
		public static Mono.Terminal.MainLoop MainLoop { get; private set; }

		static Stack<Toplevel> toplevels = new Stack<Toplevel> ();

		/// <summary>
		///   This event is raised on each iteration of the <see cref="MainLoop"/> 
		/// </summary>
		/// <remarks>
		///   See also <see cref="Timeout"/>
		/// </remarks>
		public static event EventHandler Iteration;

		/// <summary>
		/// Returns a rectangle that is centered in the screen for the provided size.
		/// </summary>
		/// <returns>The centered rect.</returns>
		/// <param name="size">Size for the rectangle.</param>
		public static Rect MakeCenteredRect (Size size)
		{
			return new Rect (new Point ((Driver.Cols - size.Width) / 2, (Driver.Rows - size.Height) / 2), size);
		}

		//
		// provides the sync context set while executing code in Terminal.Gui, to let
		// users use async/await on their code
		//
		class MainLoopSyncContext : SynchronizationContext {
			Mono.Terminal.MainLoop mainLoop;

			public MainLoopSyncContext (Mono.Terminal.MainLoop mainLoop)
			{
				this.mainLoop = mainLoop;
			}

			public override SynchronizationContext CreateCopy ()
			{
				return new MainLoopSyncContext (MainLoop);
			}

			public override void Post (SendOrPostCallback d, object state)
			{
				mainLoop.AddIdle (() => {
					d (state);
					return false;
				});
				//mainLoop.Driver.Wakeup ();
			}

			public override void Send (SendOrPostCallback d, object state)
			{
				mainLoop.Invoke (() => {
					d (state);
				});
			}
		}

		/// <summary>
		/// If set, it forces the use of the System.Console-based driver.
		/// </summary>
		public static bool UseSystemConsole;

		/// <summary>
		/// Initializes a new instance of <see cref="Terminal.Gui"/> Application. 
		/// </summary>
		/// <remarks>
		/// <para>
		/// Call this method once per instance (or after <see cref="Shutdown"/> has been called).
		/// </para>
		/// <para>
		/// Loads the right <see cref="ConsoleDriver"/> for the platform.
		/// </para>
		/// <para>
		/// Creates a <see cref="Toplevel"/> and assigns it to <see cref="Top"/> and <see cref="CurrentView"/>
		/// </para>
		/// </remarks>
		public static void Init () => Init (() => Toplevel.Create ());

		internal static bool _initialized = false;

		/// <summary>
		/// Initializes the Terminal.Gui application
		/// </summary>
		static void Init (Func<Toplevel> topLevelFactory)
		{
			if (_initialized) return;

			var p = Environment.OSVersion.Platform;
			Mono.Terminal.IMainLoopDriver mainLoopDriver;

			if (UseSystemConsole) {
				mainLoopDriver = new Mono.Terminal.NetMainLoop ();
				Driver = new NetDriver ();
			} else if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows) {
				var windowsDriver = new WindowsDriver ();
				mainLoopDriver = windowsDriver;
				Driver = windowsDriver;
			} else {
				mainLoopDriver = new Mono.Terminal.UnixMainLoop ();
				Driver = new CursesDriver ();
			}
			Driver.Init (TerminalResized);
			MainLoop = new Mono.Terminal.MainLoop (mainLoopDriver);
			SynchronizationContext.SetSynchronizationContext (new MainLoopSyncContext (MainLoop));
			Top = topLevelFactory ();
			Current = Top;
			CurrentView = Top;
			_initialized = true;
		}

		/// <summary>
		/// Captures the execution state for the provided <see cref="Toplevel"/>  view.
		/// </summary>
		public class RunState : IDisposable {
			internal RunState (Toplevel view)
			{
				Toplevel = view;
			}
			internal Toplevel Toplevel;

			/// <summary>
			/// Releases alTop = l resource used by the <see cref="Application.RunState"/> object.
			/// </summary>
			/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="Application.RunState"/>. The
			/// <see cref="Dispose()"/> method leaves the <see cref="Application.RunState"/> in an unusable state. After
			/// calling <see cref="Dispose()"/>, you must release all references to the
			/// <see cref="Application.RunState"/> so the garbage collector can reclaim the memory that the
			/// <see cref="Application.RunState"/> was occupying.</remarks>
			public void Dispose ()
			{
				Dispose (true);
				GC.SuppressFinalize (this);
			}

			/// <summary>
			/// Dispose the specified disposing.
			/// </summary>
			/// <returns>The dispose.</returns>
			/// <param name="disposing">If set to <c>true</c> disposing.</param>
			protected virtual void Dispose (bool disposing)
			{
				if (Toplevel != null) {
					End (Toplevel);
					Toplevel = null;
				}
			}
		}

		static void ProcessKeyEvent (KeyEvent ke)
		{

			var chain = toplevels.ToList ();
			foreach (var topLevel in chain) {
				if (topLevel.ProcessHotKey (ke))
					return;
				if (topLevel.Modal)
					break;
			}

			foreach (var topLevel in chain) {
				if (topLevel.ProcessKey (ke))
					return;
				if (topLevel.Modal)
					break;
			}

			foreach (var topLevel in chain) {
				// Process the key normally
				if (topLevel.ProcessColdKey (ke))
					return;
				if (topLevel.Modal)
					break;
			}
		}

		static void ProcessKeyDownEvent (KeyEvent ke)
		{
			var chain = toplevels.ToList ();
			foreach (var topLevel in chain) {
				if (topLevel.OnKeyDown (ke))
					return;
				if (topLevel.Modal)
					break;
			}
		}


		static void ProcessKeyUpEvent (KeyEvent ke)
		{
			var chain = toplevels.ToList ();
			foreach (var topLevel in chain) {
				if (topLevel.OnKeyUp (ke))
					return;
				if (topLevel.Modal)
					break;
			}
		}

		static View FindDeepestView (View start, int x, int y, out int resx, out int resy)
		{
			var startFrame = start.Frame;

			if (!startFrame.Contains (x, y)) {
				resx = 0;
				resy = 0;
				return null;
			}

			if (start.InternalSubviews != null) {
				int count = start.InternalSubviews.Count;
				if (count > 0) {
					var rx = x - startFrame.X;
					var ry = y - startFrame.Y;
					for (int i = count - 1; i >= 0; i--) {
						View v = start.InternalSubviews [i];
						if (v.Frame.Contains (rx, ry)) {
							var deep = FindDeepestView (v, rx, ry, out resx, out resy);
							if (deep == null)
								return v;
							return deep;
						}
					}
				}
			}
			resx = x - startFrame.X;
			resy = y - startFrame.Y;
			return start;
		}

		internal static View mouseGrabView;

		/// <summary>
		/// Grabs the mouse, forcing all mouse events to be routed to the specified view until UngrabMouse is called.
		/// </summary>
		/// <returns>The grab.</returns>
		/// <param name="view">View that will receive all mouse events until UngrabMouse is invoked.</param>
		public static void GrabMouse (View view)
		{
			if (view == null)
				return;
			mouseGrabView = view;
			Driver.UncookMouse ();
		}

		/// <summary>
		/// Releases the mouse grab, so mouse events will be routed to the view on which the mouse is.
		/// </summary>
		public static void UngrabMouse ()
		{
			mouseGrabView = null;
			Driver.CookMouse ();
		}

		/// <summary>
		/// Merely a debugging aid to see the raw mouse events
		/// </summary>
		public static Action<MouseEvent> RootMouseEvent;

		internal static View wantContinuousButtonPressedView;
		static View lastMouseOwnerView;

		static void ProcessMouseEvent (MouseEvent me)
		{
			var view = FindDeepestView (Current, me.X, me.Y, out int rx, out int ry);

			if (view != null && view.WantContinuousButtonPressed)
				wantContinuousButtonPressedView = view;
			else
				wantContinuousButtonPressedView = null;

			RootMouseEvent?.Invoke (me);
			if (mouseGrabView != null) {
				var newxy = mouseGrabView.ScreenToView (me.X, me.Y);
				var nme = new MouseEvent () {
					X = newxy.X,
					Y = newxy.Y,
					Flags = me.Flags,
					OfX = me.X - newxy.X,
					OfY = me.Y - newxy.Y,
					View = view
				};
				if (OutsideFrame (new Point (nme.X, nme.Y), mouseGrabView.Frame))
					lastMouseOwnerView.OnMouseLeave (me);
				if (mouseGrabView != null) {
					mouseGrabView.MouseEvent (nme);
					return;
				}
			}

			if (view != null) {
				var nme = new MouseEvent () {
					X = rx,
					Y = ry,
					Flags = me.Flags,
					OfX = rx,
					OfY = ry,
					View = view
				};

				if (lastMouseOwnerView == null) {
					lastMouseOwnerView = view;
					view.OnMouseEnter (nme);
				} else if (lastMouseOwnerView != view) {
					lastMouseOwnerView.OnMouseLeave (nme);
					view.OnMouseEnter (nme);
					lastMouseOwnerView = view;
				}

				if (!view.WantMousePositionReports && me.Flags == MouseFlags.ReportMousePosition)
					return;

				if (view.WantContinuousButtonPressed)
					wantContinuousButtonPressedView = view;
				else
					wantContinuousButtonPressedView = null;

				// Should we bubbled up the event, if it is not handled?
				view.MouseEvent (nme);
			}
		}

		static bool OutsideFrame (Point p, Rect r)
		{
			return p.X < 0 || p.X > r.Width - 1 || p.Y < 0 || p.Y > r.Height - 1;
		}

		/// <summary>
		/// This event is fired once when the application is first loaded. The dimensions of the
		/// terminal are provided.
		/// </summary>
		public static event EventHandler<ResizedEventArgs> Loaded;

		/// <summary>
		/// Building block API: Prepares the provided <see cref="Toplevel"/>  for execution.
		/// </summary>
		/// <returns>The runstate handle that needs to be passed to the <see cref="End(RunState)"/> method upon completion.</returns>
		/// <param name="toplevel">Toplevel to prepare execution for.</param>
		/// <remarks>
		///  This method prepares the provided toplevel for running with the focus,
		///  it adds this to the list of toplevels, sets up the mainloop to process the
		///  event, lays out the subviews, focuses the first element, and draws the
		///  toplevel in the screen. This is usually followed by executing
		///  the <see cref="RunLoop"/> method, and then the <see cref="End(RunState)"/> method upon termination which will
		///   undo these changes.
		/// </remarks>
		public static RunState Begin (Toplevel toplevel)
		{
			if (toplevel == null)
				throw new ArgumentNullException (nameof (toplevel));
			var rs = new RunState (toplevel);

			Init ();
			if (toplevel is ISupportInitializeNotification initializableNotification &&
			    !initializableNotification.IsInitialized) {
				initializableNotification.BeginInit ();
				initializableNotification.EndInit ();
			} else if (toplevel is ISupportInitialize initializable) {
				initializable.BeginInit ();
				initializable.EndInit ();
			}
			toplevels.Push (toplevel);
			Current = toplevel;
			Driver.PrepareToRun (MainLoop, ProcessKeyEvent, ProcessKeyDownEvent, ProcessKeyUpEvent, ProcessMouseEvent);
			if (toplevel.LayoutStyle == LayoutStyle.Computed)
				toplevel.RelativeLayout (new Rect (0, 0, Driver.Cols, Driver.Rows));
			toplevel.LayoutSubviews ();
			Loaded?.Invoke (null, new ResizedEventArgs () { Rows = Driver.Rows, Cols = Driver.Cols } );
			toplevel.WillPresent ();
			Redraw (toplevel);
			toplevel.PositionCursor ();
			Driver.Refresh ();

			return rs;
		}

		/// <summary>
		/// Building block API: completes the execution of a <see cref="Toplevel"/>  that was started with <see cref="Begin(Toplevel)"/> .
		/// </summary>
		/// <param name="runState">The runstate returned by the <see cref="Begin(Toplevel)"/> method.</param>
		public static void End (RunState runState)
		{
			if (runState == null)
				throw new ArgumentNullException (nameof (runState));

			runState.Dispose ();
			runState = null;
		}

		/// <summary>
		/// Shutdown an application initalized with <see cref="Init()"/>
		/// </summary>
		public static void Shutdown ()
		{
			// Shutdown is the bookend for Init. As such it needs to clean up all resources
			// Init created. Apps that do any threading will need to code defensively for this.
			// e.g. see Issue #537
			// TODO: Some of this state is actually related to Begin/End (not Init/Shutdown) and should be moved to `RunState` (#520)
			foreach (var t in toplevels) {
				t.Running = false;
			}
			toplevels.Clear ();
			Current = null;
			CurrentView = null;
			Top = null;

			// 
			MainLoop = null;

			Driver.End ();
			_initialized = false;
		}

		static void Redraw (View view)
		{
			Application.CurrentView = view;

			view.Redraw (view.Bounds);
			Driver.Refresh ();
		}

		static void Refresh (View view)
		{
			view.Redraw (view.Bounds);
			Driver.Refresh ();
		}

		/// <summary>
		/// Triggers a refresh of the entire display.
		/// </summary>
		public static void Refresh ()
		{
			Driver.UpdateScreen ();
			View last = null;
			foreach (var v in toplevels.Reverse ()) {
				v.SetNeedsDisplay ();
				v.Redraw (v.Bounds);
				last = v;
			}
			last?.PositionCursor ();
			Driver.Refresh ();
		}

		internal static void End (View view)
		{
			if (toplevels.Peek () != view)
				throw new ArgumentException ("The view that you end with must be balanced");
			toplevels.Pop ();
			if (toplevels.Count == 0)
				Shutdown ();
			else {
				Current = toplevels.Peek ();
				Refresh ();
			}
		}

		/// <summary>
		///   Building block API: Runs the main loop for the created dialog
		/// </summary>
		/// <remarks>
		///   Use the wait parameter to control whether this is a
		///   blocking or non-blocking call.
		/// </remarks>
		/// <param name="state">The state returned by the Begin method.</param>
		/// <param name="wait">By default this is true which will execute the runloop waiting for events, if you pass false, you can use this method to run a single iteration of the events.</param>
		public static void RunLoop (RunState state, bool wait = true)
		{
			if (state == null)
				throw new ArgumentNullException (nameof (state));
			if (state.Toplevel == null)
				throw new ObjectDisposedException ("state");

			bool firstIteration = true;
			for (state.Toplevel.Running = true; state.Toplevel.Running;) {
				if (MainLoop.EventsPending (wait)) {
					// Notify Toplevel it's ready
					if (firstIteration) {
						state.Toplevel.OnReady ();
					}
					firstIteration = false;

					MainLoop.MainIteration ();
					Iteration?.Invoke (null, EventArgs.Empty);
				} else if (wait == false)
					return;
				if (state.Toplevel.NeedDisplay != null && (!state.Toplevel.NeedDisplay.IsEmpty || state.Toplevel.childNeedsDisplay)) {
					state.Toplevel.Redraw (state.Toplevel.Bounds);
					if (DebugDrawBounds)
						DrawBounds (state.Toplevel);
					state.Toplevel.PositionCursor ();
					Driver.Refresh ();
				} else
					Driver.UpdateCursor ();
			}
		}

		internal static bool DebugDrawBounds = false;

		// Need to look into why this does not work properly.
		static void DrawBounds (View v)
		{
			v.DrawFrame (v.Frame, padding: 0, fill: false);
			if (v.InternalSubviews != null && v.InternalSubviews.Count > 0)
				foreach (var sub in v.InternalSubviews)
					DrawBounds (sub);
		}

		/// <summary>
		/// Runs the application by calling <see cref="Run(Toplevel)"/> with the value of <see cref="Top"/>
		/// </summary>
		public static void Run ()
		{
			Run (Top);
		}

		/// <summary>
		/// Runs the application by calling <see cref="Run(Toplevel)"/> with a new instance of the specified <see cref="Toplevel"/>-derived class
		/// </summary>
		public static void Run<T> () where T : Toplevel, new()
		{
			Init (() => new T ());
			Run (Top);
		}

		/// <summary>
		///   Runs the main loop on the given <see cref="Toplevel"/> container.
		/// </summary>
		/// <remarks>
		///   <para>
		///     This method is used to start processing events
		///     for the main application, but it is also used to
		///     run other modal <see cref="View"/>s such as <see cref="Dialog"/> boxes.
		///   </para>
		///   <para>
		///     To make a <see cref="Run(Toplevel)"/> stop execution, call <see cref="Application.RequestStop"/>.
		///   </para>
		///   <para>
		///     Calling <see cref="Run(Toplevel)"/> is equivalent to calling <see cref="Begin(Toplevel)"/>, followed by <see cref="RunLoop(RunState, bool)"/>,
		///     and then calling <see cref="End(RunState)"/>.
		///   </para>
		///   <para>
		///     Alternatively, to have a program control the main loop and 
		///     process events manually, call <see cref="Begin(Toplevel)"/> to set things up manually and then
		///     repeatedly call <see cref="RunLoop(RunState, bool)"/> with the wait parameter set to false.   By doing this
		///     the <see cref="RunLoop(RunState, bool)"/> method will only process any pending events, timers, idle handlers and
		///     then return control immediately.
		///   </para>
		/// </remarks>
		public static void Run (Toplevel view)
		{
			var runToken = Begin (view);
			RunLoop (runToken);
			End (runToken);
		}

		/// <summary>
		/// Stops running the most recent <see cref="Toplevel"/>. 
		/// </summary>
		/// <remarks>
		///   <para>
		///   This will cause <see cref="Application.Run()"/> to return.
		///   </para>
		///   <para>
		///     Calling <see cref="Application.RequestStop"/> is equivalent to setting the <see cref="Toplevel.Running"/> property on the curently running <see cref="Toplevel"/> to false.
		///   </para>
		/// </remarks>
		public static void RequestStop ()
		{
			Current.Running = false;
		}

		/// <summary>
		/// Event arguments for the <see cref="Application.Resized"/> event.
		/// </summary>
		public class ResizedEventArgs : EventArgs {
			/// <summary>
			/// The number of rows in the resized terminal.
			/// </summary>
			public int Rows { get; set; }
			/// <summary>
			/// The number of columns in the resized terminal.
			/// </summary>
			public int Cols { get; set; }
		}

		/// <summary>
		/// Invoked when the terminal was resized. The new size of the terminal is provided.
		/// </summary>
		public static event EventHandler<ResizedEventArgs> Resized;

		static void TerminalResized ()
		{
			var full = new Rect (0, 0, Driver.Cols, Driver.Rows);
			Resized?.Invoke (null, new ResizedEventArgs () { Cols = full.Width, Rows = full.Height });
			Driver.Clip = full;
			foreach (var t in toplevels) {
				t.PositionToplevels ();
				t.RelativeLayout (full);
				t.LayoutSubviews ();
			}
			Refresh ();
		}
	}
}
