
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.VideoStab;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Cvb;
using Emgu.CV.Util;
using PersonsBase.myStd;

namespace WebCamEmguCv
{
    public partial class SnapshotForm : Form
    {
        private  EmguCv _myCv;
        private Image _capturedPersonImg;
        private int scroll = 70;
        public SnapshotForm()
        {
            InitializeComponent();
            _myCv = new EmguCv();
            _capturedPersonImg = null;
        }

        // Форма загрузилась
        private void SnapshotForm_Load(object sender, EventArgs e)
        {
            try
            {
                int numCam = Int32.Parse(comboBox1.SelectedItem.ToString());


                if (!_myCv.TryInitCamera(numCam))
                {
                    MessageBox.Show(@"Ошибка камеры. Попробуйте переподключить камеру");
                    return;
                }

            }
            catch (Exception exception)
            {
                MessageBox.Show(@"Ошибка камеры. Попробуйте переподключить камеру");
                return;
            }


            _myCv.StartCapturing();
            _myCv.FrameChanged += RunTimePhotoFace;
            button_TakePicture.Enabled = true;
        }

        private void RunTimePhotoFace(object sender, EventArgs e)
        {
            pictureBox_Original.Image = _myCv.GetImageWithFaces(_myCv.FrameMat, true, true, true);
            Image<Gray, Byte> grayImage = _myCv.FrameImage.Clone().Clone().Resize(300, 200, Inter.Linear).Convert<Gray, Byte>();
            pictureBox_gray.Image = grayImage.ToBitmap();

            Image<Gray, Byte> binaryImage = grayImage.ThresholdBinary(new Gray(scroll), new Gray(255));
            pictureBox_black.Image = binaryImage.ToBitmap();

            GC.Collect();
        }


        /// <summary>
        /// Главный метод. Вызывается после закрытия формы. Возвращает сделанный снимок. Или возвращает null
        ///
        /// </summary>
        /// <returns></returns>
        public Image GetImage()
        {
            return _capturedPersonImg;
        }

        private void button_TakePicture_Click(object sender, EventArgs e)
        {
            button_Ok.Enabled = true;
            button_TakePicture.Enabled = false;
            try
            {
                Image<Bgr, byte> imageTemp = _myCv.FrameImage.Clone();

                var imgFaces = FindFacesIfNeeded(imageTemp, true);

                _capturedPersonImg = (Bitmap)imgFaces.ToBitmap();
                pictureBox_Final.Image = (Bitmap)_capturedPersonImg.Clone();
            }
            catch (Exception exception)
            {
                MessageBox.Show(@"Ошибка сохранения2 " + exception.Message);
            }
            Thread.Sleep(100);
            button_TakePicture.Enabled = true;

        }

        private Image<Bgr, byte> FindFacesIfNeeded(Image<Bgr, byte> img, bool isDetectorEn)
        {
            Image<Bgr, byte> imageTemp = img;

            // Если не нужно искать лица
            if (!isDetectorEn) return imageTemp;

            using (var curCv = new EmguCv())
            {
                var grayImage = img.Convert<Gray, Byte>();

                var facesList = curCv.GetFaces(grayImage);
                if (facesList.Count > 1)
                {
                    MessageBox.Show(@"Ошибка поиска или в кадре несколько лиц!");
                    curCv.DrawRectangleOnImage(facesList, ref imageTemp, Color.Blue);
                    button_Ok.Enabled = false;
                }
                else
                {
                    button_Ok.Enabled = true;
                }
                curCv.DisposeAll();
            }
            return imageTemp.Clone();
        }

        private void button_Cancel_Click(object sender, EventArgs e)
        {
            Close();
            GC.Collect();
        }

        private void SnapshotForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _myCv.DisposeAll();
            GC.Collect();
        }

        private void trackBar1_Scroll_1(object sender, EventArgs e)
        {
            scroll = trackBar1.Value;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem == null) return;
          
            _myCv.DisposeAll();
            _myCv = new EmguCv();

            int numCam = Int32.Parse(comboBox1.SelectedItem.ToString());


            if (!_myCv.TryInitCamera(numCam))
            {
                MessageBox.Show(@"Ошибка камеры. Попробуйте переподключить камеру");
                return;
            }
            _myCv.StartCapturing();
            _myCv.FrameChanged += RunTimePhotoFace;
            button_TakePicture.Enabled = true;
        }
    }
}
