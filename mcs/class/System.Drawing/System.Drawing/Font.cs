//
// System.Drawing.Fonts.cs
//
// Authors:
//	Alexandre Pigolkine (pigolkine@gmx.de)
//	Miguel de Icaza (miguel@ximian.com)
//	Todd Berman (tberman@sevenl.com)
//	Jordi Mas i Hernandez (jordi@ximian.com)
//	Ravindra (rkumar@novell.com)
//
// Copyright (C) 2004 Ximian, Inc. (http://www.ximian.com)
// Copyright (C) 2004, 2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace System.Drawing
{
	[Serializable]
	[ComVisible (true)]
	[Editor ("System.Drawing.Design.FontEditor, " + Consts.AssemblySystem_Drawing_Design, typeof (System.Drawing.Design.UITypeEditor))]
	[TypeConverter (typeof (FontConverter))]
	public sealed class Font : MarshalByRefObject, ISerializable, ICloneable, IDisposable
	{
		private IntPtr	fontObject = IntPtr.Zero;
		private string  systemFontName;
		private string  originalFontName;
		private float _size;
		private object olf;

		private const byte DefaultCharSet = 1;
		private static int CharSetOffset = -1;

		private void CreateFont (string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte charSet, bool isVertical)
		{
			originalFontName = familyName;
                        FontFamily family;
			// NOTE: If family name is null, empty or invalid,
			// MS creates Microsoft Sans Serif font.
			try {
				family = new FontFamily (familyName);
			}
			catch (Exception){
				family = FontFamily.GenericSansSerif;
			}

			setProperties (family, emSize, style, unit, charSet, isVertical);
			Status status = GDIPlus.GdipCreateFont (family.NativeFamily, emSize,  style, unit, out fontObject);

			if (status == Status.FontStyleNotFound)
				throw new ArgumentException (Locale.GetText ("Style {0} isn't supported by font {1}.", style.ToString (), familyName));

			GDIPlus.CheckStatus (status);
		}

       		private Font (SerializationInfo info, StreamingContext context)
		{
			string		name;
			float		size;
			FontStyle	style;
			GraphicsUnit	unit;

			name = (string)info.GetValue("Name", typeof(string));
			size = (float)info.GetValue("Size", typeof(float));
			style = (FontStyle)info.GetValue("Style", typeof(FontStyle));
			unit = (GraphicsUnit)info.GetValue("Unit", typeof(GraphicsUnit));

			CreateFont(name, size, style, unit, DefaultCharSet, false);
		}

		void ISerializable.GetObjectData(SerializationInfo si, StreamingContext context)
		{
			si.AddValue("Name", Name);
			si.AddValue ("Size", Size);
			si.AddValue ("Style", Style);
			si.AddValue ("Unit", Unit);
		}

		~Font()
		{
			Dispose ();
		}

		public void Dispose ()
		{
			if (fontObject != IntPtr.Zero) {
				Status status = GDIPlus.GdipDeleteFont (fontObject);
				fontObject = IntPtr.Zero;
				GC.SuppressFinalize (this);
				// check the status code (throw) at the last step
				GDIPlus.CheckStatus (status);
			}
		}

		internal void SetSystemFontName (string newSystemFontName)
		{
			systemFontName = newSystemFontName;
		}

		internal void unitConversion (GraphicsUnit fromUnit, GraphicsUnit toUnit, float nSrc, out float nTrg)
		{
			float inchs = 0;
			nTrg = 0;

			switch (fromUnit) {
			case GraphicsUnit.Display:
				inchs = nSrc / 75f;
				break;
			case GraphicsUnit.Document:
				inchs = nSrc / 300f;
				break;
			case GraphicsUnit.Inch:
				inchs = nSrc;
				break;
			case GraphicsUnit.Millimeter:
				inchs = nSrc / 25.4f;
				break;
			case GraphicsUnit.Pixel:
			case GraphicsUnit.World:
				inchs = nSrc / Graphics.systemDpiX;
				break;
			case GraphicsUnit.Point:
				inchs = nSrc / 72f;
				break;
			default:
				throw new ArgumentException("Invalid GraphicsUnit");
			}

			switch (toUnit) {
			case GraphicsUnit.Display:
				nTrg = inchs * 75;
				break;
			case GraphicsUnit.Document:
				nTrg = inchs * 300;
				break;
			case GraphicsUnit.Inch:
				nTrg = inchs;
				break;
			case GraphicsUnit.Millimeter:
				nTrg = inchs * 25.4f;
				break;
			case GraphicsUnit.Pixel:
			case GraphicsUnit.World:
				nTrg = inchs * Graphics.systemDpiX;
				break;
			case GraphicsUnit.Point:
				nTrg = inchs * 72;
				break;
			default:
				throw new ArgumentException("Invalid GraphicsUnit");
			}
		}

		void setProperties (FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte charSet, bool isVertical)
		{
			_name = family.Name;
			_fontFamily = family;
			_size = emSize;

			// MS throws ArgumentException, if unit is set to GraphicsUnit.Display
			_unit = unit;
			_style = style;
			_gdiCharSet = charSet;
			_gdiVerticalFont = isVertical;

			unitConversion (unit, GraphicsUnit.Point, emSize, out  _sizeInPoints);

			_bold = _italic = _strikeout = _underline = false;

                        if ((style & FontStyle.Bold) == FontStyle.Bold)
                                _bold = true;

                        if ((style & FontStyle.Italic) == FontStyle.Italic)
                               _italic = true;

                        if ((style & FontStyle.Strikeout) == FontStyle.Strikeout)
                                _strikeout = true;

                        if ((style & FontStyle.Underline) == FontStyle.Underline)
                                _underline = true;
		}

		public static Font FromHfont (IntPtr hfont)
		{
			IntPtr			newObject;
			IntPtr			hdc;
			FontStyle		newStyle = FontStyle.Regular;
			float			newSize;
			LOGFONT			lf = new LOGFONT ();

			// Sanity. Should we throw an exception?
			if (hfont == IntPtr.Zero) {
				Font result = new Font ("Arial", (float)10.0, FontStyle.Regular);
				return(result);
			}

			if (GDIPlus.RunningOnUnix ()) {
				// If we're on Unix we use our private gdiplus API to avoid Wine
				// dependencies in S.D
				Status s = GDIPlus.GdipCreateFontFromHfont (hfont, out newObject, ref lf);
				GDIPlus.CheckStatus (s);
			} else {

				// This needs testing
				// GetDC, SelectObject, ReleaseDC GetTextMetric and
				// GetFontFace are not really GDIPlus, see gdipFunctions.cs

				newStyle = FontStyle.Regular;

				hdc = GDIPlus.GetDC (IntPtr.Zero);
				try {
					return FromLogFont (lf, hdc);
				}
				finally {
					GDIPlus.ReleaseDC (IntPtr.Zero, hdc);
				}
			}

			if (lf.lfItalic != 0) {
				newStyle |= FontStyle.Italic;
			}

			if (lf.lfUnderline != 0) {
				newStyle |= FontStyle.Underline;
			}

			if (lf.lfStrikeOut != 0) {
				newStyle |= FontStyle.Strikeout;
			}

			if (lf.lfWeight > 400) {
				newStyle |= FontStyle.Bold;
			}

			if (lf.lfHeight < 0) {
				newSize = lf.lfHeight * -1;
			} else {
				newSize = lf.lfHeight;
			}

			return (new Font (newObject, lf.lfFaceName, newStyle, newSize));
		}

		public IntPtr ToHfont ()
		{
			if (fontObject == IntPtr.Zero)
				throw new ArgumentException (Locale.GetText ("Object has been disposed."));

			if (GDIPlus.RunningOnUnix ())
				return fontObject;

			// win32 specific code
			if (olf == null) {
				olf = new LOGFONT ();
				ToLogFont(olf);
			}
			LOGFONT lf = (LOGFONT)olf;
			return GDIPlus.CreateFontIndirect (ref lf);
		}

		internal Font (IntPtr newFontObject, string familyName, FontStyle style, float size)
		{
			FontFamily fontFamily;

			try {
				fontFamily = new FontFamily (familyName);
			}
			catch (Exception){
				fontFamily = FontFamily.GenericSansSerif;
			}

			setProperties (fontFamily, size, style, GraphicsUnit.Pixel, 0, false);
			fontObject = newFontObject;
		}

		public Font (Font prototype, FontStyle newStyle)
		{
			// no null checks, MS throws a NullReferenceException if original is null
			setProperties (prototype.FontFamily, prototype.Size, newStyle, prototype.Unit, prototype.GdiCharSet, prototype.GdiVerticalFont);

			Status status = GDIPlus.GdipCreateFont (_fontFamily.NativeFamily, Size, Style, Unit, out fontObject);
			GDIPlus.CheckStatus (status);
		}

		public Font (FontFamily family, float emSize,  GraphicsUnit unit)
			: this (family, emSize, FontStyle.Regular, unit, DefaultCharSet, false)
		{
		}

		public Font (string familyName, float emSize,  GraphicsUnit unit)
			: this (new FontFamily (familyName), emSize, FontStyle.Regular, unit, DefaultCharSet, false)
		{
		}

		public Font (FontFamily family, float emSize)
			: this (family, emSize, FontStyle.Regular, GraphicsUnit.Point, DefaultCharSet, false)
		{
		}

		public Font (FontFamily family, float emSize, FontStyle style)
			: this (family, emSize, style, GraphicsUnit.Point, DefaultCharSet, false)
		{
		}

		public Font (FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
			: this (family, emSize, style, unit, DefaultCharSet, false)
		{
		}

		public Font (FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
			: this (family, emSize, style, unit, gdiCharSet, false)
		{
		}

		public Font (FontFamily family, float emSize, FontStyle style,
				GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
		{
			if (family == null)
				throw new ArgumentNullException ("family");

			Status status;
			setProperties (family, emSize, style, unit, gdiCharSet,  gdiVerticalFont );
			status = GDIPlus.GdipCreateFont (family.NativeFamily, emSize,  style,   unit,  out fontObject);
			GDIPlus.CheckStatus (status);
		}

		public Font (string familyName, float emSize)
			: this (familyName, emSize, FontStyle.Regular, GraphicsUnit.Point, DefaultCharSet, false)
		{
		}

		public Font (string familyName, float emSize, FontStyle style)
			: this (familyName, emSize, style, GraphicsUnit.Point, DefaultCharSet, false)
		{
		}

		public Font (string familyName, float emSize, FontStyle style, GraphicsUnit unit)
			: this (familyName, emSize, style, unit, DefaultCharSet, false)
		{
		}

		public Font (string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
			: this (familyName, emSize, style, unit, gdiCharSet, false)
		{
		}

		public Font (string familyName, float emSize, FontStyle style,
				GraphicsUnit unit, byte gdiCharSet, bool  gdiVerticalFont )
		{
			CreateFont (familyName, emSize, style, unit, gdiCharSet,  gdiVerticalFont );
		}
		internal Font (string familyName, float emSize, string systemName)
			: this (familyName, emSize, FontStyle.Regular, GraphicsUnit.Point, DefaultCharSet, false)
		{
			systemFontName = systemName;
		}
		public object Clone ()
		{
			return new Font (this, Style);
		}

		internal IntPtr NativeObject {
			get {
				return fontObject;
			}
		}

		private bool _bold;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public bool Bold {
			get {
				return _bold;
			}
		}

		private FontFamily _fontFamily;

		[Browsable (false)]
		public FontFamily FontFamily {
			get {
				return _fontFamily;
			}
		}

		private byte _gdiCharSet;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public byte GdiCharSet {
			get {
				return _gdiCharSet;
			}
		}

		private bool _gdiVerticalFont;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public bool GdiVerticalFont {
			get {
				return _gdiVerticalFont;
			}
		}

		[Browsable (false)]
		public int Height {
			get {
				return (int) Math.Ceiling (GetHeight ());
			}
		}

		[Browsable(false)]
		public bool IsSystemFont {
			get {
				return !string.IsNullOrEmpty (systemFontName);
			}
		}

		private bool _italic;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public bool Italic {
			get {
				return _italic;
			}
		}

		private string _name;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		[Editor ("System.Drawing.Design.FontNameEditor, " + Consts.AssemblySystem_Drawing_Design, typeof (System.Drawing.Design.UITypeEditor))]
		[TypeConverter (typeof (FontConverter.FontNameConverter))]
		public string Name {
			get {
				return _name;
			}
		}

		public float Size {
			get {
				return _size;
			}
		}

		private float _sizeInPoints;

		[Browsable (false)]
		public float SizeInPoints {
			get {
				return _sizeInPoints;
			}
		}

		private bool _strikeout;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public bool Strikeout {
			get {
				return _strikeout;
			}
		}

		private FontStyle _style;

		[Browsable (false)]
		public FontStyle Style {
			get {
				return _style;
			}
		}

		[Browsable(false)]
		public string SystemFontName {
			get {
				return systemFontName;
			}
		}

		[Browsable(false)]
		public string OriginalFontName {
			get {
				return originalFontName;
			}
		}
		private bool _underline;

		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public bool Underline {
			get {
				return _underline;
			}
		}

		private GraphicsUnit _unit;

		[TypeConverter (typeof (FontConverter.FontUnitConverter))]
		public GraphicsUnit Unit {
			get {
				return _unit;
			}
		}

		public override bool Equals (object obj)
		{
			Font fnt = (obj as Font);
			if (fnt == null)
				return false;

			if (fnt.FontFamily.Equals (FontFamily) && fnt.Size == Size &&
			    fnt.Style == Style && fnt.Unit == Unit &&
			    fnt.GdiCharSet == GdiCharSet &&
			    fnt.GdiVerticalFont == GdiVerticalFont)
				return true;
			else
				return false;
		}

		private int _hashCode;

		public override int GetHashCode ()
		{
			if (_hashCode == 0) {
				_hashCode = 17;
				unchecked {
					_hashCode = _hashCode * 23 + _name.GetHashCode();
					_hashCode = _hashCode * 23 + FontFamily.GetHashCode();
					_hashCode = _hashCode * 23 + _size.GetHashCode();
					_hashCode = _hashCode * 23 + _unit.GetHashCode();
					_hashCode = _hashCode * 23 + _style.GetHashCode();
					_hashCode = _hashCode * 23 + _gdiCharSet;
					_hashCode = _hashCode * 23 + _gdiVerticalFont.GetHashCode();
				}
			}

			return _hashCode;
		}

		[MonoTODO ("The hdc parameter has no direct equivalent in libgdiplus.")]
		public static Font FromHdc (IntPtr hdc)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO ("The returned font may not have all it's properties initialized correctly.")]
		public static Font FromLogFont (object lf, IntPtr hdc)
		{
			IntPtr newObject;
			LOGFONT o = (LOGFONT)lf;
			Status status = GDIPlus.GdipCreateFontFromLogfont (hdc, ref o, out newObject);
			GDIPlus.CheckStatus (status);
			return new Font (newObject, "Microsoft Sans Serif", FontStyle.Regular, 10);
		}

		public float GetHeight ()
		{
			return GetHeight (Graphics.systemDpiY);
		}

		public static Font FromLogFont (object lf)
		{
			if (GDIPlus.RunningOnUnix ())
				return FromLogFont(lf, IntPtr.Zero);

			// win32 specific code
			IntPtr hDC = IntPtr.Zero;
			try {
				hDC = GDIPlus.GetDC(IntPtr.Zero);
				return FromLogFont (lf, hDC);
			}
			finally {
				GDIPlus.ReleaseDC (IntPtr.Zero, hDC);
			}
		}

		public void ToLogFont (object logFont)
		{
			if (GDIPlus.RunningOnUnix ()) {
				// Unix - We don't have a window we could associate the DC with
				// so we use an image instead
				using (Bitmap img = new Bitmap (1, 1, Imaging.PixelFormat.Format32bppArgb)) {
					using (Graphics g = Graphics.FromImage (img)) {
						ToLogFont (logFont, g);
					}
				}
			} else {
				// Windows
				IntPtr hDC = GDIPlus.GetDC (IntPtr.Zero);
				try {
					using (Graphics g = Graphics.FromHdc (hDC)) {
						ToLogFont (logFont, g);
					}
				}
				finally {
					GDIPlus.ReleaseDC (IntPtr.Zero, hDC);
				}
			}
		}

		public void ToLogFont (object logFont, Graphics graphics)
		{
			if (graphics == null)
				throw new ArgumentNullException ("graphics");

			if (logFont == null) {
				throw new AccessViolationException ("logFont");
			}

			Type st = logFont.GetType ();
			if (!st.GetTypeInfo ().IsLayoutSequential)
				throw new ArgumentException ("logFont", Locale.GetText ("Layout must be sequential."));

			// note: there is no exception if 'logFont' isn't big enough
			Type lf = typeof (LOGFONT);
			int size = Marshal.SizeOf (logFont);
			if (size >= Marshal.SizeOf (lf)) {
				Status status;
				IntPtr copy = Marshal.AllocHGlobal (size);
				try {
					Marshal.StructureToPtr (logFont, copy, false);

					status = GDIPlus.GdipGetLogFont (NativeObject, graphics.NativeObject, logFont);
					if (status != Status.Ok) {
						// reset to original values
						Marshal.PtrToStructure (copy, logFont);
					}
				}
				finally {
					Marshal.FreeHGlobal (copy);
				}

				if (CharSetOffset == -1) {
					// not sure why this methods returns an IntPtr since it's an offset
					// anyway there's no issue in downcasting the result into an int32
					CharSetOffset = (int) Marshal.OffsetOf (lf, "lfCharSet");
				}

				// note: Marshal.WriteByte(object,*) methods are unimplemented on Mono
                IntPtr gch = Marshal.AllocHGlobal (size);
				try {
                    Marshal.StructureToPtr (logFont, copy, false);

					// if GDI+ lfCharSet is 0, then we return (S.D.) 1, otherwise the value is unchanged
					if (Marshal.ReadByte (gch, CharSetOffset) == 0) {
						// set lfCharSet to 1
						Marshal.WriteByte (gch, CharSetOffset, 1);
					}
				}
				finally
                {
                    Marshal.FreeHGlobal(gch);
				}

				// now we can throw, if required
				GDIPlus.CheckStatus (status);
			}
		}

		public float GetHeight (Graphics graphics)
		{
			if (graphics == null)
				throw new ArgumentNullException ("graphics");

			float size;
			Status status = GDIPlus.GdipGetFontHeight (fontObject, graphics.NativeObject, out size);
			GDIPlus.CheckStatus (status);
			return size;
		}

		public float GetHeight (float dpi)
		{
			float size;
			Status status = GDIPlus.GdipGetFontHeightGivenDPI (fontObject, dpi, out size);
			GDIPlus.CheckStatus (status);
			return size;
		}

		public override String ToString ()
		{
			return String.Format ("[Font: Name={0}, Size={1}, Units={2}, GdiCharSet={3}, GdiVerticalFont={4}]", _name, Size, (int)_unit, _gdiCharSet, _gdiVerticalFont);
		}
	}
}
