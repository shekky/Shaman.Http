#if NET35
//
// MultipartContent.cs
//
// Authors:
//	Marek Safar  <marek.safar@gmail.com>
//
// Copyright (C) 2012 Xamarin Inc (http://www.xamarin.com)
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

using System.IO;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Net;
using Shaman;
using Shaman.Runtime;

namespace System.Net.Http
{
	public class MultipartContent : HttpContent, IEnumerable<HttpContent>
	{
		List<HttpContent> nested_content;
		readonly string boundary;

		public MultipartContent ()
			: this ("mixed")
		{
		}

		public MultipartContent (string subtype)
			: this (subtype, Guid.NewGuid ().ToString ("D", CultureInfo.InvariantCulture))
		{
		}

		public MultipartContent (string subtype, string boundary)
		{
			if (HttpUtils.IsNullOrWhiteSpace (subtype))
				throw new ArgumentException ("boundary");

			//
			// The only mandatory parameter for the multipart Content-Type is the boundary parameter, which consists
			// of 1 to 70 characters from a set of characters known to be very robust through email gateways,
			// and NOT ending with white space
			//
			if (HttpUtils.IsNullOrWhiteSpace (boundary))
				throw new ArgumentException ("boundary");

			if (boundary.Length > 70)
				throw new ArgumentOutOfRangeException ("boundary");

			if (boundary.Last () == ' ' || !IsValidRFC2049 (boundary))
				throw new ArgumentException ("boundary");

			this.boundary = boundary;
			this.nested_content = new List<HttpContent> (2);

			ContentType = "multipart/" + subtype + "; boundary=\""+boundary+"\"";
		}

		static bool IsValidRFC2049 (string s)
		{
			foreach (char c in s) {
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
					continue;

				switch (c) {
				case '\'': case '(': case ')': case '+': case ',':
				case '-': case '.': case '/': case ':': case '=':
				case '?':
					continue;
				}

				return false;
			}

			return true;
		}

		public virtual void Add (HttpContent content)
		{
			if (content == null)
				throw new ArgumentNullException ("content");

			if (nested_content == null)
				nested_content = new List<HttpContent> ();

			nested_content.Add (content);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				foreach (var item in nested_content) {
					item.Dispose ();
				}
				
				nested_content = null;
			}
			
			base.Dispose (disposing);
		}

		protected internal override async Task SerializeToStreamAsync (Stream stream, TransportContext context)
		{
			// RFC 2046
			//
			// The Content-Type field for multipart entities requires one parameter,
			// "boundary". The boundary delimiter line is then defined as a line
			// consisting entirely of two hyphen characters ("-", decimal value 45)
			// followed by the boundary parameter value from the Content-Type header
			// field, optional linear whitespace, and a terminating CRLF.
			//

			byte[] buffer;
			var sb = new StringBuilder ();
			sb.Append ('-').Append ('-');
			sb.Append (boundary);
			sb.Append ('\r').Append ('\n');

			for (int i = 0; i < nested_content.Count; i++) {
				var c = nested_content [i];
		        
                
                if(c.ContentType != null) 
                {
					sb.Append ("Content-Type: ");
			        sb.Append (c.ContentType);
					sb.Append ('\r').Append ('\n');
                }
                
                
                if(c.ContentDisposition != null) 
                {
					sb.Append ("Content-Disposition: ");
			        sb.Append (c.ContentDisposition);
					sb.Append ('\r').Append ('\n');
                }
				sb.Append ('\r').Append ('\n');
					
				buffer = Encoding.ASCII.GetBytes (sb.ToString ());
				sb.Length = 0;
				await stream.WriteAsync (buffer, 0, buffer.Length).ConfigureAwait (false);

				await c.SerializeToStreamAsync (stream, context).ConfigureAwait (false);
					
				if (i != nested_content.Count - 1) {
					sb.Append ('\r').Append ('\n');
					sb.Append ('-').Append ('-');
					sb.Append (boundary);
					sb.Append ('\r').Append ('\n');
				}
			}
			
			sb.Append ('\r').Append ('\n');
			sb.Append ('-').Append ('-');
			sb.Append (boundary);
			sb.Append ('-').Append ('-');
			sb.Append ('\r').Append ('\n');

			buffer = Encoding.ASCII.GetBytes (sb.ToString ());
			await stream.WriteAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
		}
		
		protected internal override bool TryComputeLength (out long length)
		{
			length = 12 + 2 * boundary.Length;
			
			for (int i = 0; i < nested_content.Count; i++) {
				var c = nested_content [i];
                if(c.ContentType != null) length += 4 + "Content-Type".Length + c.ContentType.Length;
                if(c.ContentDisposition != null) length += 4 + "Content-Disposition".Length + c.ContentDisposition.Length;
					
				long l;
				if (!c.TryComputeLength (out l))
					return false;

				length += 2;
				length += l;
					
				if (i != nested_content.Count - 1) {
					length += 6;
					length += boundary.Length;
				}
			}

			return true;
		}

		public IEnumerator<HttpContent> GetEnumerator ()
		{
			return nested_content.GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return nested_content.GetEnumerator ();
		}
	}
}
#endif