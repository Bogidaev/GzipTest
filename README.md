# Тестовое задание   
  Написать консольную программу на C#, предназначенную для поблочного сжатия и
расжатия файлов с помощью System.IO.Compression.GzipStream.

  Для компрессии исходный файл делится на блоки одинакового размера, например, в 1
мегабайт. Каждый блок компрессится и записывается в выходной файл независимо от
остальных блоков.

  Программа должна эффективно распараллеливать и синхронизировать обработку блоков
в многопроцессорной среде и уметь обрабатывать файлы, размер которых превышает
объем доступной оперативной памяти.

  В случае исключительных ситуаций необходимо проинформировать пользователя
понятным сообщением, позволяющим пользователю исправить возникшую проблему, в
частности если проблемы связаны с ограничениями операционной системы.
При работе с потоками допускается использовать только стандартные классы и
библиотеки из .Net 3.5 (исключая ThreadPool, BackgroundWorker, TPL). Ожидается
реализация с использованием Thread-ов.
Код программы должен соответствовать принципам ООП и ООД (читаемость, разбиение
на классы и т.д.).

  Параметры программы, имена исходного и результирующего файлов должны задаваться
в командной строке следующим образом:
GZipTest.exe compress/decompress [имя исходного файла] [имя результирующего файла]
