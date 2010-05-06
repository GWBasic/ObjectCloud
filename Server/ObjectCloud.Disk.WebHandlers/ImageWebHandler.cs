// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Adds additional web-based image processing capabilities
    /// </summary>
    public class ImageWebHandler : BinaryWebHandler
    {
        /// <summary>
        /// Returns a scaled version of the image to fit the constraints passed in
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="width">The width.  If height is not specified, then a proportional height will be used</param>
        /// <param name="height">The width.  If width is not specified, then a proportiional width will be used</param>
        /// <param name="maxHeight">The maxiumum height to return</param>
        /// <param name="maxWidth">The maximum width to return</param>
        /// <param name="minHeight">The mimumum height to return</param>
        /// <param name="minWidth">The minumum width to return</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Naked, FilePermissionEnum.Read)]
        public IWebResults GetScaled(
            IWebConnection webConnection,
            int? width,
            int? height,
            int? maxWidth,
            int? maxHeight,
            int? minWidth,
            int? minHeight)
        {
            int returnedWidth;
            int returnedHeight;

            double aspectRatio = Convert.ToDouble(Image.Width) / Convert.ToDouble(Image.Height);
            double inverseAspectRatio = Convert.ToDouble(Image.Height) / Convert.ToDouble(Image.Width);

            // First, figure out what the size of the returned image will be based on specified height or width

            if ((null != width) && (null != height))
            {
                returnedHeight = height.Value;
                returnedWidth = width.Value;
                aspectRatio = Convert.ToDouble(returnedWidth) / Convert.ToDouble(returnedHeight);
                inverseAspectRatio = Convert.ToDouble(returnedHeight) / Convert.ToDouble(returnedWidth);
            }
            else if (null != width)
            {
                returnedWidth = width.Value;
                returnedHeight = Convert.ToInt32(
                    Convert.ToDouble(width.Value) * inverseAspectRatio);
            }
            else if (null != height)
            {
                returnedHeight = height.Value;
                returnedWidth = Convert.ToInt32(
                    Convert.ToDouble(height.Value) * aspectRatio);
            }
            else
            {
                returnedWidth = Image.Width;
                returnedHeight = Image.Height;
            }

            // Second, make sure that maxes and mins aren't violated

            if (null != maxWidth)
                if (returnedWidth > maxWidth.Value)
                {
                    returnedWidth = maxWidth.Value;
                    returnedHeight = Convert.ToInt32(
                        Convert.ToDouble(maxWidth.Value) * inverseAspectRatio);
                }

            if (null != maxHeight)
                if (returnedHeight > maxHeight.Value)
                {
                    returnedHeight = maxHeight.Value;
                    returnedWidth = Convert.ToInt32(
                        Convert.ToDouble(maxHeight.Value) * aspectRatio);
                }

            if (null != minWidth)
                if (returnedWidth < minWidth.Value)
                {
                    returnedWidth = minWidth.Value;
                    returnedHeight = Convert.ToInt32(
                        Convert.ToDouble(minWidth.Value) * inverseAspectRatio);
                }

            if (null != minHeight)
                if (returnedHeight < minHeight.Value)
                {
                    returnedHeight = minHeight.Value;
                    returnedWidth = Convert.ToInt32(
                        Convert.ToDouble(minHeight.Value) * aspectRatio);
                }

            // Now, in the rare chance that size doesn't change, don't do anything
            // (disabled due to MIME ambiguities)
            //if ((returnedWidth == Image.Width) && (returnedHeight == Image.Height))
            //    return ReadAll(webConnection);

            // The image must be resized.


            // Check to see if there is a cached version
            string cacheKey = "resized_w_" + returnedWidth + "_h_" + returnedHeight;
            byte[] resizedImageBytes;
            IWebResults toReturn;
            if (FileHandler.TryGetCached(cacheKey, out resizedImageBytes))
            {
                MemoryStream stream = new MemoryStream(resizedImageBytes, false);
                toReturn = WebResults.FromStream(Status._200_OK, stream);
            }
            else
            {
                // There wasn't a cached version.  The resizing must occur

                // reference: http://www.glennjones.net/Post/799/Highqualitydynamicallyresizedimageswithnet.htm

                Image thumbnail = new Bitmap(returnedWidth, returnedHeight);
                Graphics graphic = System.Drawing.Graphics.FromImage(thumbnail);

                // Set up high-quality resize mode
                graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphic.SmoothingMode = SmoothingMode.HighQuality;
                graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphic.CompositingQuality = CompositingQuality.HighQuality;

                // do the resize
                graphic.DrawImage(Image, 0, 0, returnedWidth, returnedHeight);

                // Save as a high quality JPEG
                ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();
                EncoderParameters encoderParameters;
                encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

                MemoryStream ms = new MemoryStream();
                thumbnail.Save(ms, info[1], encoderParameters);
                resizedImageBytes = new byte[ms.Length];

				// Saved the resized image in the cache
				ms.Seek(0, SeekOrigin.Begin);
				ms.Read(resizedImageBytes, 0, resizedImageBytes.Length);
                FileHandler.SetCached(cacheKey, resizedImageBytes);

                ms.Seek(0, SeekOrigin.Begin);
                toReturn = WebResults.FromStream(Status._200_OK, ms);
            }

            toReturn.ContentType = "image/jpeg";
            return toReturn;
            //Image.
        }

        /// <summary>
        /// Im-memory object that encapsulates a saved image.  This allows for programmatic access to metadata
        /// </summary>
        public Image Image
        {
            get
            {
                // If the image isn't in memory, then load it
                if (null == _Image)
                    using (TimedLock.Lock(FileHandler))
                    {
                        using (MemoryStream ms = new MemoryStream(FileHandler.ReadAll()))
                            _Image = Image.FromStream(ms);

                        FileHandler.ContentsChanged += new EventHandler<IBinaryHandler, EventArgs>(FileHandler_ContentsChanged);
                    }

                return _Image;
            }
        }
        private Image _Image = null;

        // If the image changes, then the in-memory image needs to be unloaded
        void FileHandler_ContentsChanged(IBinaryHandler sender, EventArgs e)
        {
            FileHandler.ContentsChanged -= new EventHandler<IBinaryHandler, EventArgs>(FileHandler_ContentsChanged);
            _Image = null;
        }
    }
}
