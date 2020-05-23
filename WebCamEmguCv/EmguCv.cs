using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
//using DirectShowLib;

using Emgu.CV;
using Emgu.Util;
using Emgu.CV.VideoStab;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Cvb;
using Emgu.CV.ImgHash;
using Emgu.CV.Ocl;
using Emgu.CV.Util;
using Gray = Emgu.CV.Structure.Gray;


namespace PersonsBase.myStd
{
    public class EmguCv : IDisposable
    {
        #region /// СОБЫТИЯ ///
        /// <summary>
        /// Событие при изменении картинки CameraBitmap
        /// </summary>
        [field: NonSerialized] public event EventHandler FrameChanged;
        private void OnFrameChanged()
        {
            FrameChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region /// ПОЛЯ ///
        private VideoCapture _capture;

        //Pre-trained cascade
        private readonly CascadeClassifier _faceCascade;
        private readonly CascadeClassifier _eyeCascade;
        private Image<Bgr, Byte> _frameImage;

        // Готовое чистое изображение с камеры
        public Mat _frameMat;

        public Image<Bgr, byte> FrameImage
        {
            get
            {
                return _frameImage;
            }
            private set
            {
                _frameImage = value;
                OnFrameChanged();
            }
        }

        public Mat FrameMat
        {
            get { return _frameMat; }
            set { _frameMat = value; }
        }

        #endregion

        #region /// КОНСТРУКТОРЫ

        public EmguCv()
        {
            FrameMat = new Mat();
            if (File.Exists("haarcascade_frontalface_default.xml"))
                _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
            if (File.Exists("haarcascade_eye.xml"))
                _eyeCascade = new CascadeClassifier("haarcascade_eye.xml");
        }

        ~EmguCv()
        {
            DisposeAll();
        }

        public void DisposeAll()
        {
            FrameMat?.Dispose();
            _capture?.Stop();
            _capture?.Dispose();
            _capture = null;
            GC.Collect();
        }

        #endregion

        #region /// МЕТОДЫ ИНИЦИАЛИЗАЦИИ

        /// <summary>
        /// Устанавливает параметры камеры. Обязателен к запуску. Возвращает True если успешно
        /// Возвращает False если ошибка либо если камеры не найдено
        /// </summary>
        /// <param name="cameraId"></param>
        /// <returns></returns>
        public bool TryInitCamera(int cameraId)
        {
            var result = false;
            try
            {
                if (_capture == null)
                {
                    _capture = new VideoCapture(cameraId);

                    _capture.SetCaptureProperty(CapProp.FrameWidth, 1024);
                    _capture.SetCaptureProperty(CapProp.FrameHeight, 768);
                   // _capture.SetCaptureProperty(CapProp.Exposure, -2);
                   // _capture.SetCaptureProperty(CapProp.Fps, 15);
                    _capture.FlipHorizontal = true;
                    _capture.ImageGrabbed += ProcessFrame;
                }
                result = true;
            }
            catch
            {
                MessageBox.Show(
                    @"Ошибка Стартовой инициализации камеры. Попробуйте переподключить камеру и перезапустить сьемку");
                _capture?.Stop();
                _capture?.Dispose();
                _capture = null;
            }

            return result;
        }

        /// <summary>
        /// Запускает процесс постоянной сьемки. Генерируются события при получении каждого кадра.
        /// Новый кадр изображения будет добавлен в FrameBitmap. При этом будет событие FrameChanged
        /// </summary>
        public void StartCapturing()
        {
            if (_capture == null) return;
            if (!_capture.IsOpened) return;

            try
            {
                _capture?.Start();
            }
            catch (Exception ex)
            {
                _capture?.Dispose();
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Вызывается когда получен новый фрейм с камеры. Копирует изображение в FrameBitmap.
        /// Так же копирует маленькое изображение в _smallFrame
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProcessFrame(object sender, EventArgs e)
        {//FIXME тут есть баг с вылетанием программы.
            if (_capture != null && _capture.Ptr != IntPtr.Zero && _capture.IsOpened)
            {
                try
                {
                    _capture.Retrieve(FrameMat, 0);// Вылетает где-то тут. ДОступ к памяти
                    FrameImage = FrameMat.Clone().ToImage<Bgr, Byte>();
                    Thread.Sleep(50);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        @"Ошибка обработки текущего фрейма изображения. Попробуйте перезапустить сьемку");
                    DisposeAll();
                }
            }
        }

        #endregion

        #region /// МЕТОДЫ ОБРАБОТКИ ИЗОБРАЖЕНИЙ

        /// <summary>
        /// Возвращает Изображение с Выделенными лицами, если лица найдены. Иначе просто изображение
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="showEye"></param>
        /// <returns></returns>
        public Bitmap GetImageWithFaces(Mat frameInput, bool showEye, bool drawRect, bool reduceSizeImg)
        {
            Mat frame = frameInput.Clone();
            Image<Bgr, Byte> image = null;
            try
            {
                if (drawRect)
                {
                    image = FaceDetection(frame, showEye, true, reduceSizeImg);
                }
                else
                {
                    image = reduceSizeImg ? frame.ToImage<Bgr, Byte>().Resize(450, 250, Inter.Linear) : frame.ToImage<Bgr, Byte>();
                }
            }
            catch (Exception e)
            {
                image = new Image<Bgr, byte>(1, 1);
                MessageBox.Show(@"Ошибка поиска лица 1");
            }
            GC.Collect();
            return image.ToBitmap();
        }

        private Image<Bgr, byte> FaceDetection(Mat frame, bool showEye, bool drawRect, bool reduceSizeImg)
        {
            //Load the image
            Image<Bgr, Byte> image = reduceSizeImg ? frame.ToImage<Bgr, Byte>().Resize(450, 250, Inter.Linear) : frame.ToImage<Bgr, Byte>();

            var imgResult = FaceDetection(image, showEye, drawRect, reduceSizeImg);

            return imgResult;
        }

        private Image<Bgr, byte> FaceDetection(Image<Bgr, Byte> imageInput, bool showEye, bool drawRect, bool reduceSizeImg)
        {
            Image<Bgr, Byte> image = reduceSizeImg ? imageInput.Clone().Resize(450, 250, Inter.Linear) : imageInput.Clone();

            //The input image of Cascadeclassifier must be grayscale
            Image<Gray, Byte> grayImage = image.Convert<Gray, Byte>();
            //Use List to store faces and eyes
            var facesList = GetFaces(grayImage);
            var eyesList = showEye ? GetEyesList(facesList, grayImage) : new List<Rectangle>();

            if (drawRect)
            {
                DrawRectangleOnImage(facesList, ref image, Color.Red);
                DrawRectangleOnImage(eyesList, ref image, Color.Blue);
            }
            GC.Collect();
            //Show image
            return image;
        }

        public void DrawRectangleOnImage(List<Rectangle> listRectangles, ref Image<Bgr, byte> image, Color colr)
        {
            //Draw detected area
            foreach (Rectangle face1 in listRectangles)
                image.Draw(face1, new Bgr(colr), 1);
        }

        public List<Rectangle> GetFaces(Image<Gray, byte> grayImage)
        {
            //Face detection
            var facesDetectedList = _faceCascade?.DetectMultiScale(
                grayImage, //image
                1.1, //scaleFactor
                10, //minNeighbors
                new Size(20, 20), //minSize
                Size.Empty); //maxSize
            GC.Collect();
            if (facesDetectedList != null) return facesDetectedList.ToList<Rectangle>();
            else
            {
                return new List<Rectangle>();
            }
        }

        private List<Rectangle> GetEyesList(List<Rectangle> facesDetected, Image<Gray, byte> gray)
        {
            var eyesList = new List<Rectangle>();
            //Eyes detection
            foreach (Rectangle f in facesDetected)
            {
                gray.ROI = f;
                Rectangle[] eyesDetected = _eyeCascade?.DetectMultiScale(
                    gray,
                    1.1,
                    10,
                    new Size(20, 20),
                    Size.Empty);
                gray.ROI = Rectangle.Empty;
                if (eyesDetected != null)
                    foreach (Rectangle ey in eyesDetected)
                    {
                        Rectangle eyeRect = ey;
                        eyeRect.Offset(f.X, f.Y);
                        eyesList.Add(eyeRect);
                    }
            }
            GC.Collect();
            return eyesList;
        }

        #endregion

        public void Dispose()
        {
            DisposeAll();
        }
    }
}
///// <summary> СПИСОК КАМЕР. НЕ ТЕСТИРОВАНО
///// Возвращает список камер,доступных в системе. Индекс- Имя
///// </summary>
///// <returns></returns>
//public List<KeyValuePair<int, string>> GetCamersList()
//{
//    CvInvoke.UseOpenCL = false;
//    var sysCams = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

//    var camList = sysCams.Select((camera, camIndex) => new KeyValuePair<int, string>(camIndex, camera.Name)).ToList();
//    return camList;
//}

/*
 private Image<Bgr, byte> FaceDetection(Mat frame)
        {
            //Load the image
            Image<Bgr, Byte> image = frame.ToImage<Bgr, Byte>();

            //Use List to store faces and eyes
            var faces = new List<System.Drawing.Rectangle>();
            var eyes = new List<Rectangle>();
            //Pre-trained cascade
            var face = new CascadeClassifier("haarcascade_frontalface_default.xml");
            var eye = new CascadeClassifier("haarcascade_eye.xml");
            //The input image of Cascadeclassifier must be grayscale
            Image<Gray, Byte> gray = image.Convert<Gray, Byte>();
            //Face detection
            var facesDetected = face.DetectMultiScale(
                gray, //image
                1.1, //scaleFactor
                10, //minNeighbors
                new Size(20, 20), //minSize
                Size.Empty); //maxSize
            faces.AddRange(facesDetected);
            //Eyes detection
            foreach (Rectangle f in facesDetected)
            {
                gray.ROI = f;
                Rectangle[] eyesDetected = eye.DetectMultiScale(
                    gray,
                    1.1,
                    10,
                    new Size(20, 20),
                    Size.Empty);
                gray.ROI = Rectangle.Empty;
                foreach (Rectangle ey in eyesDetected)
                {
                    Rectangle eyeRect = ey;
                    eyeRect.Offset(f.X, f.Y);
                    eyes.Add(eyeRect);
                }
            }

            //Draw detected area
            foreach (Rectangle face1 in faces)
                image.Draw(face1, new Bgr(Color.Red), 2);
            foreach (Rectangle eye1 in eyes)
                image.Draw(eye1, new Bgr(Color.Blue), 2);

            GC.Collect();
            //Show image
            return image;
        }



 */
