using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GdiBrush = System.Drawing.Brush;
using GdiColor = System.Drawing.Color;
using GdiPen = System.Drawing.Pen;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;

namespace CertHarvester
{
	/// <summary>
	/// Статический генератор векторных иконок Lucide (lucide.dev)
	/// Выполняет чистую программную отрисовку по базовой сетке 24x24 без сторонних библиотек и внешних файлов.
	/// </summary>
	public static class LucideIcons
	{
		private static readonly Dictionary<string, Bitmap> _bitmapCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, ImageSource> _imageSourceCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Создает объект GDI+ Bitmap заданного размера и цвета с векторной геометрией иконки Lucide
		/// </summary>
		public static Bitmap CreateBitmap(string iconName, int size, GdiColor color)
		{
			if (string.IsNullOrWhiteSpace(iconName))
			{
				iconName = "monitor";
			}

			if (size <= 0)
			{
				size = 16;
			}

			string cacheKey = string.Format("{0}_{1}_{2}", iconName.Trim().ToLowerInvariant(), size, color.ToArgb());
			lock (_bitmapCache)
			{
				if (_bitmapCache.TryGetValue(cacheKey, out Bitmap cached) && cached != null)
				{
					return (Bitmap)cached.Clone();
				}

				Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;

					float scale = size / 24f;
					g.ScaleTransform(scale, scale);

					using (GdiPen pen = new GdiPen(color, 2f))
					{
						pen.StartCap = LineCap.Round;
						pen.EndCap = LineCap.Round;
						pen.LineJoin = LineJoin.Round;

						DrawVectorShape(g, iconName.Trim().ToLowerInvariant(), pen, color);
					}
				}

				_bitmapCache[cacheKey] = bmp;
				return (Bitmap)bmp.Clone();
			}
		}

		/// <summary>
		/// Преобразует GDI+ Bitmap в WPF ImageSource с сохранением прозрачности
		/// </summary>
		public static ImageSource BitmapToImageSource(Bitmap bitmap)
		{
			if (bitmap == null)
			{
				return null;
			}

			using (MemoryStream ms = new MemoryStream())
			{
				bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
				ms.Position = 0;

				BitmapImage bi = new BitmapImage();
				bi.BeginInit();
				bi.CacheOption = BitmapCacheOption.OnLoad;
				bi.StreamSource = ms;
				bi.EndInit();
				bi.Freeze();
				return bi;
			}
		}

		/// <summary>
		/// Создает замороженный объект WPF ImageSource для иконки Lucide
		/// </summary>
		public static ImageSource CreateImageSource(string iconName, int size, GdiColor color)
		{
			if (string.IsNullOrWhiteSpace(iconName))
			{
				iconName = "monitor";
			}

			string cacheKey = string.Format("{0}_{1}_{2}", iconName.Trim().ToLowerInvariant(), size, color.ToArgb());
			lock (_imageSourceCache)
			{
				if (_imageSourceCache.TryGetValue(cacheKey, out ImageSource cached) && cached != null)
				{
					return cached;
				}

				using (Bitmap bmp = CreateBitmap(iconName, size, color))
				{
					ImageSource source = BitmapToImageSource(bmp);
					_imageSourceCache[cacheKey] = source;
					return source;
				}
			}
		}

		/// <summary>
		/// Создает WPF ImageSource из цвета WPF Color
		/// </summary>
		public static ImageSource CreateImageSource(string iconName, int size, WpfColor color)
		{
			return CreateImageSource(iconName, size, GdiColor.FromArgb(color.A, color.R, color.G, color.B));
		}

		/// <summary>
		/// Создает WPF ImageSource из WPF кисти Brush
		/// </summary>
		public static ImageSource CreateImageSource(string iconName, int size, WpfBrush brush)
		{
			return CreateImageSource(iconName, size, ExtractGdiColor(brush));
		}

		/// <summary>
		/// Создает элемент System.Windows.Controls.Image с нужной векторной иконкой Lucide
		/// </summary>
		public static System.Windows.Controls.Image CreateWpfImage(string iconName, int size, GdiColor color)
		{
			return new System.Windows.Controls.Image
			{
				Source = CreateImageSource(iconName, size, color),
				Width = size,
				Height = size,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				SnapsToDevicePixels = true
			};
		}

		/// <summary>
		/// Создает элемент System.Windows.Controls.Image из WPF цвета
		/// </summary>
		public static System.Windows.Controls.Image CreateWpfImage(string iconName, int size, WpfColor color)
		{
			return CreateWpfImage(iconName, size, GdiColor.FromArgb(color.A, color.R, color.G, color.B));
		}

		/// <summary>
		/// Создает элемент System.Windows.Controls.Image из WPF кисти
		/// </summary>
		public static System.Windows.Controls.Image CreateWpfImage(string iconName, int size, WpfBrush brush)
		{
			return CreateWpfImage(iconName, size, ExtractGdiColor(brush));
		}

		/// <summary>
		/// Формирует горизонтальный контейнер с иконкой и текстом, выровненный по центру (ImageBeforeText)
		/// </summary>
		public static StackPanel CreateButtonContent(string iconName, string text, int iconSize = 16, GdiColor? iconColor = null, GdiColor? textColor = null, double spacing = 8, FontWeight? fontWeight = null)
		{
			StackPanel panel = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};

			GdiColor iColor = iconColor ?? GdiColor.White;
			System.Windows.Controls.Image img = CreateWpfImage(iconName, iconSize, iColor);
			img.Margin = new Thickness(0, 0, spacing, 0);
			panel.Children.Add(img);

			if (!string.IsNullOrEmpty(text))
			{
				TextBlock lbl = new TextBlock
				{
					Text = text,
					VerticalAlignment = VerticalAlignment.Center
				};

				if (textColor.HasValue)
				{
					lbl.Foreground = new SolidColorBrush(WpfColor.FromArgb(textColor.Value.A, textColor.Value.R, textColor.Value.G, textColor.Value.B));
				}

				if (fontWeight.HasValue)
				{
					lbl.FontWeight = fontWeight.Value;
				}

				panel.Children.Add(lbl);
			}

			return panel;
		}

		/// <summary>
		/// Формирует горизонтальный контейнер с иконкой и текстом с поддержкой WPF-кистей
		/// </summary>
		public static StackPanel CreateButtonContent(string iconName, string text, int iconSize, WpfBrush iconBrush, WpfBrush textBrush, double spacing = 8, FontWeight? fontWeight = null)
		{
			GdiColor iColor = ExtractGdiColor(iconBrush);
			GdiColor tColor = ExtractGdiColor(textBrush);
			return CreateButtonContent(iconName, text, iconSize, iColor, tColor, spacing, fontWeight);
		}

		/// <summary>
		/// Устанавливает центрированный контент (иконка слева + текст, ImageBeforeContent) в кнопку WPF
		/// </summary>
		public static void ApplyToButton(System.Windows.Controls.Button button, string iconName, string text, int iconSize = 16, GdiColor? iconColor = null, GdiColor? textColor = null, double spacing = 8, FontWeight? fontWeight = null)
		{
			if (button == null)
			{
				return;
			}

			button.Content = CreateButtonContent(iconName, text, iconSize, iconColor, textColor, spacing, fontWeight);
		}

		/// <summary>
		/// Устанавливает центрированный контент в кнопку WPF с использованием WPF-кистей
		/// </summary>
		public static void ApplyToButton(System.Windows.Controls.Button button, string iconName, string text, int iconSize, WpfBrush iconBrush, WpfBrush textBrush, double spacing = 8, FontWeight? fontWeight = null)
		{
			if (button == null)
			{
				return;
			}

			button.Content = CreateButtonContent(iconName, text, iconSize, iconBrush, textBrush, spacing, fontWeight);
		}

		/// <summary>
		/// Устанавливает векторный Bitmap и выравнивание ImageBeforeText для кнопок Windows Forms
		/// </summary>
		public static void ApplyToWinFormsButton(System.Windows.Forms.Button button, string iconName, int size, GdiColor color)
		{
			if (button == null)
			{
				return;
			}

			button.Image = CreateBitmap(iconName, size, color);
			button.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			button.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
		}

		/// <summary>
		/// Извлекает GDI+ Color из WPF кисти Brush
		/// </summary>
		public static GdiColor ExtractGdiColor(WpfBrush brush)
		{
			if (brush is SolidColorBrush scb)
			{
				return GdiColor.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
			}

			return GdiColor.White;
		}

		/// <summary>
		/// Основной диспетчер отрисовки векторных путей Lucide на координатной сетке 24x24
		/// </summary>
		private static void DrawVectorShape(Graphics g, string name, GdiPen pen, GdiColor color)
		{
			switch (name)
			{
				case "sliders-horizontal":
				case "slidershorizontal":
				case "sliders":
				case "settings":
					DrawSlidersHorizontal(g, pen);
					break;

				case "search":
					DrawSearch(g, pen);
					break;

				case "globe":
					DrawGlobe(g, pen);
					break;

				case "monitor":
				case "computer":
					DrawMonitor(g, pen);
					break;

				case "folder":
					DrawFolder(g, pen);
					break;

				case "zap":
				case "flash":
				case "lightning":
					DrawZap(g, pen, color);
					break;

				case "database":
				case "db":
					DrawDatabase(g, pen);
					break;

				case "trash-2":
				case "trash2":
				case "trash":
				case "delete":
					DrawTrash2(g, pen);
					break;

				case "play":
				case "start":
					DrawPlay(g, pen, color);
					break;

				case "square":
				case "stop":
					DrawSquare(g, pen, color);
					break;

				case "sun":
					DrawSun(g, pen);
					break;

				case "moon":
					DrawMoon(g, pen);
					break;

				case "save":
				case "disk":
					DrawSave(g, pen);
					break;

				case "shield-check":
				case "shieldcheck":
				case "shield":
					DrawShieldCheck(g, pen);
					break;

				case "plug":
				case "connect":
					DrawPlug(g, pen);
					break;

				case "check":
					DrawCheck(g, pen);
					break;

				case "x":
				case "close":
					DrawX(g, pen);
					break;

				case "plus":
				case "add":
					DrawPlus(g, pen);
					break;

				case "key":
					DrawKey(g, pen);
					break;

				case "user":
					DrawUser(g, pen);
					break;

				case "alert-triangle":
				case "warning":
					DrawAlertTriangle(g, pen);
					break;

				case "alert-circle":
				case "error":
					DrawAlertCircle(g, pen);
					break;

				case "help-circle":
				case "question":
					DrawHelpCircle(g, pen);
					break;

				case "smartphone":
				case "phone":
					DrawSmartphone(g, pen);
					break;

				case "refresh-cw":
				case "refresh":
					DrawRefreshCw(g, pen);
					break;

				default:
					DrawMonitor(g, pen);
					break;
			}
		}

		#region Отрисовка геометрии Lucide на сетке 24x24

		// 1) SlidersHorizontal: 3 горизонтальные линии на y=4, y=12, y=20 с засечками ползунков
		private static void DrawSlidersHorizontal(Graphics g, GdiPen pen)
		{
			// Верхняя линия (y=4) и засечка на x=14
			g.DrawLine(pen, 3f, 4f, 10f, 4f);
			g.DrawLine(pen, 14f, 4f, 21f, 4f);
			g.DrawLine(pen, 14f, 2f, 14f, 6f);

			// Средняя линия (y=12) и засечка на x=8
			g.DrawLine(pen, 3f, 12f, 8f, 12f);
			g.DrawLine(pen, 12f, 12f, 21f, 12f);
			g.DrawLine(pen, 8f, 10f, 8f, 14f);

			// Нижняя линия (y=20) и засечка на x=16
			g.DrawLine(pen, 3f, 20f, 12f, 20f);
			g.DrawLine(pen, 16f, 20f, 21f, 20f);
			g.DrawLine(pen, 16f, 18f, 16f, 22f);
		}

		// 2) Search: окружность с центром 11,11 радиусом 8 и ручка
		private static void DrawSearch(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 3f, 3f, 16f, 16f);
			g.DrawLine(pen, 16.65f, 16.65f, 21f, 21f);
		}

		// 3) Globe: окружность с центром 12,12 радиусом 10, экватор и меридианы
		private static void DrawGlobe(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 2f, 2f, 20f, 20f);
			g.DrawLine(pen, 2f, 12f, 22f, 12f);
			g.DrawEllipse(pen, 6.5f, 2f, 11f, 20f);
		}

		// 4) Monitor: скругленный экран 20x14, ножка и основание
		private static void DrawMonitor(Graphics g, GdiPen pen)
		{
			DrawRoundedRectangle(g, pen, 2f, 3f, 20f, 14f, 2f);
			g.DrawLine(pen, 12f, 17f, 12f, 21f);
			g.DrawLine(pen, 8f, 21f, 16f, 21f);
		}

		// 5) Folder: контур папки с ярлычком сверху
		private static void DrawFolder(Graphics g, GdiPen pen)
		{
			using (GraphicsPath path = new GraphicsPath())
			{
				path.AddLine(2f, 6f, 2f, 18f);
				path.AddArc(2f, 16f, 4f, 4f, 90f, 90f);
				path.AddLine(4f, 20f, 20f, 20f);
				path.AddArc(18f, 16f, 4f, 4f, 0f, 90f);
				path.AddLine(22f, 18f, 22f, 8f);
				path.AddArc(18f, 6f, 4f, 4f, 270f, 90f);
				path.AddLine(20f, 6f, 12.5f, 6f);
				path.AddLine(10.5f, 4f, 4f, 4f);
				path.AddArc(2f, 4f, 4f, 4f, 180f, 90f);
				path.CloseFigure();
				g.DrawPath(pen, path);
			}
		}

		// 6) Zap: полигон молнии (13,2 - 3,14 - 12,14 - 11,22 - 21,10 - 12,10)
		private static void DrawZap(Graphics g, GdiPen pen, GdiColor color)
		{
			PointF[] pts = new PointF[]
			{
				new PointF(13f, 2f),
				new PointF(3f, 14f),
				new PointF(12f, 14f),
				new PointF(11f, 22f),
				new PointF(21f, 10f),
				new PointF(12f, 10f)
			};

			using (SolidBrush fillBrush = new SolidBrush(GdiColor.FromArgb(80, color.R, color.G, color.B)))
			{
				g.FillPolygon(fillBrush, pts);
			}

			g.DrawPolygon(pen, pts);
		}

		// 7) Database: верхний эллипс, боковые линии и две дуги
		private static void DrawDatabase(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 3f, 2f, 18f, 6f);
			g.DrawLine(pen, 3f, 5f, 3f, 19f);
			g.DrawLine(pen, 21f, 5f, 21f, 19f);
			g.DrawArc(pen, 3f, 9f, 18f, 6f, 0f, 180f);
			g.DrawArc(pen, 3f, 16f, 18f, 6f, 0f, 180f);
		}

		// 8) Trash2: крышка, верхняя ручка, корпус корзины и внутренние линии
		private static void DrawTrash2(Graphics g, GdiPen pen)
		{
			g.DrawLine(pen, 3f, 6f, 21f, 6f);

			using (GraphicsPath handle = new GraphicsPath())
			{
				handle.AddLine(8f, 6f, 8f, 4f);
				handle.AddArc(8f, 2f, 4f, 4f, 180f, 90f);
				handle.AddLine(10f, 2f, 14f, 2f);
				handle.AddArc(12f, 2f, 4f, 4f, 270f, 90f);
				handle.AddLine(16f, 4f, 16f, 6f);
				g.DrawPath(pen, handle);
			}

			using (GraphicsPath body = new GraphicsPath())
			{
				body.AddLine(5f, 6f, 5f, 19f);
				body.AddArc(5f, 17f, 4f, 4f, 90f, 90f);
				body.AddLine(7f, 21f, 17f, 21f);
				body.AddArc(15f, 17f, 4f, 4f, 0f, 90f);
				body.AddLine(19f, 19f, 19f, 6f);
				g.DrawPath(pen, body);
			}

			g.DrawLine(pen, 10f, 11f, 10f, 17f);
			g.DrawLine(pen, 14f, 11f, 14f, 17f);
		}

		// 9) Play: треугольник запуска
		private static void DrawPlay(Graphics g, GdiPen pen, GdiColor color)
		{
			PointF[] pts = new PointF[]
			{
				new PointF(6f, 4f),
				new PointF(19f, 12f),
				new PointF(6f, 20f)
			};

			using (SolidBrush fill = new SolidBrush(color))
			{
				g.FillPolygon(fill, pts);
			}

			g.DrawPolygon(pen, pts);
		}

		// Square: квадрат остановки
		private static void DrawSquare(Graphics g, GdiPen pen, GdiColor color)
		{
			using (SolidBrush fill = new SolidBrush(color))
			{
				FillRoundedRectangle(g, fill, 5f, 5f, 14f, 14f, 2f);
			}

			DrawRoundedRectangle(g, pen, 5f, 5f, 14f, 14f, 2f);
		}

		// 10) Sun: круг с 8 лучами
		private static void DrawSun(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 8f, 8f, 8f, 8f);

			g.DrawLine(pen, 12f, 2f, 12f, 4f);
			g.DrawLine(pen, 12f, 20f, 12f, 22f);
			g.DrawLine(pen, 2f, 12f, 4f, 12f);
			g.DrawLine(pen, 20f, 12f, 22f, 12f);

			g.DrawLine(pen, 4.93f, 4.93f, 6.34f, 6.34f);
			g.DrawLine(pen, 17.66f, 17.66f, 19.07f, 19.07f);
			g.DrawLine(pen, 4.93f, 19.07f, 6.34f, 17.66f);
			g.DrawLine(pen, 17.66f, 6.34f, 19.07f, 4.93f);
		}

		// Moon: полумесяц
		private static void DrawMoon(Graphics g, GdiPen pen)
		{
			using (GraphicsPath moonPath = new GraphicsPath())
			{
				moonPath.AddArc(3f, 3f, 18f, 18f, 270f, -90f);
				moonPath.AddArc(9f, 3f, 12f, 12f, 90f, 90f);
				moonPath.CloseFigure();
				g.DrawPath(pen, moonPath);
			}
		}

		// 11) Save: дискета параметров БД
		private static void DrawSave(Graphics g, GdiPen pen)
		{
			using (GraphicsPath disk = new GraphicsPath())
			{
				disk.AddLine(5f, 3f, 15f, 3f);
				disk.AddLine(15f, 3f, 21f, 9f);
				disk.AddLine(21f, 9f, 21f, 19f);
				disk.AddArc(17f, 17f, 4f, 4f, 0f, 90f);
				disk.AddLine(19f, 21f, 5f, 21f);
				disk.AddArc(3f, 17f, 4f, 4f, 90f, 90f);
				disk.AddLine(3f, 19f, 3f, 5f);
				disk.AddArc(3f, 3f, 4f, 4f, 180f, 90f);
				disk.CloseFigure();
				g.DrawPath(pen, disk);
			}

			g.DrawLine(pen, 7f, 21f, 7f, 13f);
			g.DrawLine(pen, 7f, 13f, 17f, 13f);
			g.DrawLine(pen, 17f, 13f, 17f, 21f);

			g.DrawLine(pen, 7f, 3f, 7f, 8f);
			g.DrawLine(pen, 7f, 8f, 14f, 8f);
		}

		// ShieldCheck: щит с галочкой
		private static void DrawShieldCheck(Graphics g, GdiPen pen)
		{
			using (GraphicsPath shield = new GraphicsPath())
			{
				shield.AddLine(12f, 2f, 19f, 5f);
				shield.AddLine(19f, 5f, 20f, 11f);
				shield.AddBezier(20f, 11f, 20f, 17f, 16f, 20.5f, 12f, 22f);
				shield.AddBezier(12f, 22f, 8f, 20.5f, 4f, 17f, 4f, 11f);
				shield.AddLine(4f, 11f, 5f, 5f);
				shield.CloseFigure();
				g.DrawPath(pen, shield);
			}

			g.DrawLine(pen, 9f, 12f, 11f, 14f);
			g.DrawLine(pen, 11f, 14f, 15f, 9f);
		}

		// Plug: штекер подключения
		private static void DrawPlug(Graphics g, GdiPen pen)
		{
			g.DrawLine(pen, 12f, 17f, 12f, 22f);
			g.DrawLine(pen, 9f, 2f, 9f, 7f);
			g.DrawLine(pen, 15f, 2f, 15f, 7f);

			using (GraphicsPath plug = new GraphicsPath())
			{
				plug.AddLine(6f, 7f, 18f, 7f);
				plug.AddLine(18f, 7f, 18f, 12f);
				plug.AddArc(10f, 8f, 8f, 8f, 0f, 90f);
				plug.AddLine(14f, 16f, 10f, 16f);
				plug.AddArc(6f, 8f, 8f, 8f, 90f, 90f);
				plug.CloseFigure();
				g.DrawPath(pen, plug);
			}
		}

		// Check: галочка
		private static void DrawCheck(Graphics g, GdiPen pen)
		{
			g.DrawLine(pen, 4f, 12f, 9f, 17f);
			g.DrawLine(pen, 9f, 17f, 20f, 6f);
		}

		// X: крестик
		private static void DrawX(Graphics g, GdiPen pen)
		{
			g.DrawLine(pen, 6f, 6f, 18f, 18f);
			g.DrawLine(pen, 18f, 6f, 6f, 18f);
		}

		// Plus: плюс
		private static void DrawPlus(Graphics g, GdiPen pen)
		{
			g.DrawLine(pen, 12f, 5f, 12f, 19f);
			g.DrawLine(pen, 5f, 12f, 19f, 12f);
		}

		// Key: ключ
		private static void DrawKey(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 3.5f, 11.5f, 8f, 8f);
			g.DrawLine(pen, 9f, 13f, 20f, 4f);
			g.DrawLine(pen, 17f, 7f, 19f, 9f);
			g.DrawLine(pen, 14f, 10f, 16f, 12f);
		}

		// User: профиль пользователя
		private static void DrawUser(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 8f, 3f, 8f, 8f);
			g.DrawArc(pen, 4f, 14f, 16f, 14f, 180f, 180f);
		}

		// AlertTriangle: треугольник предупреждения
		private static void DrawAlertTriangle(Graphics g, GdiPen pen)
		{
			using (GraphicsPath tri = new GraphicsPath())
			{
				tri.AddLine(12f, 3f, 21.73f, 19f);
				tri.AddArc(19.73f, 17f, 4f, 4f, 0f, 90f);
				tri.AddLine(20f, 21f, 4f, 21f);
				tri.AddArc(2f, 17f, 4f, 4f, 90f, 90f);
				tri.CloseFigure();
				g.DrawPath(pen, tri);
			}

			g.DrawLine(pen, 12f, 9f, 12f, 13f);
			g.DrawEllipse(pen, 11.5f, 16.5f, 1f, 1f);
		}

		// AlertCircle: круг ошибки
		private static void DrawAlertCircle(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 2f, 2f, 20f, 20f);
			g.DrawLine(pen, 12f, 7f, 12f, 13f);
			g.DrawEllipse(pen, 11.5f, 16.5f, 1f, 1f);
		}

		// HelpCircle: круг вопроса
		private static void DrawHelpCircle(Graphics g, GdiPen pen)
		{
			g.DrawEllipse(pen, 2f, 2f, 20f, 20f);
			using (GraphicsPath q = new GraphicsPath())
			{
				q.AddArc(9f, 6f, 6f, 6f, 180f, 180f);
				q.AddBezier(15f, 9f, 15f, 11f, 12f, 11f, 12f, 14f);
				g.DrawPath(pen, q);
			}

			g.DrawEllipse(pen, 11.5f, 17f, 1f, 1f);
		}

		// Smartphone: смартфон
		private static void DrawSmartphone(Graphics g, GdiPen pen)
		{
			DrawRoundedRectangle(g, pen, 5f, 2f, 14f, 20f, 2f);
			g.DrawLine(pen, 11f, 18f, 13f, 18f);
		}

		// RefreshCw: круговые стрелки
		private static void DrawRefreshCw(Graphics g, GdiPen pen)
		{
			g.DrawArc(pen, 3f, 3f, 18f, 18f, 45f, 180f);
			g.DrawLine(pen, 21f, 12f, 21f, 7f);
			g.DrawLine(pen, 21f, 7f, 16f, 7f);

			g.DrawArc(pen, 3f, 3f, 18f, 18f, 225f, 180f);
			g.DrawLine(pen, 3f, 12f, 3f, 17f);
			g.DrawLine(pen, 3f, 17f, 8f, 17f);
		}

		private static void DrawRoundedRectangle(Graphics g, GdiPen pen, float x, float y, float width, float height, float radius)
		{
			using (GraphicsPath path = new GraphicsPath())
			{
				path.AddArc(x, y, radius * 2, radius * 2, 180f, 90f);
				path.AddLine(x + radius, y, x + width - radius, y);
				path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270f, 90f);
				path.AddLine(x + width, y + radius, x + width, y + height - radius);
				path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0f, 90f);
				path.AddLine(x + width - radius, y + height, x + radius, y + height);
				path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90f, 90f);
				path.CloseFigure();
				g.DrawPath(pen, path);
			}
		}

		private static void FillRoundedRectangle(Graphics g, GdiBrush brush, float x, float y, float width, float height, float radius)
		{
			using (GraphicsPath path = new GraphicsPath())
			{
				path.AddArc(x, y, radius * 2, radius * 2, 180f, 90f);
				path.AddLine(x + radius, y, x + width - radius, y);
				path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270f, 90f);
				path.AddLine(x + width, y + radius, x + width, y + height - radius);
				path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0f, 90f);
				path.AddLine(x + width - radius, y + height, x + radius, y + height);
				path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90f, 90f);
				path.CloseFigure();
				g.FillPath(brush, path);
			}
		}

		#endregion
	}
}
