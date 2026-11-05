# Технические ссылки для UI

- **Avalonia transitions:** https://docs.avaloniaui.net/docs/graphics-animation/control-transitions. XAML transitions запускаются при изменении заданных свойств. В проекте заданы короткие переходы фона/рамки кнопок и лёгкий hover-shift карточки; бесконечной декоративной анимации нет.
- **SVG renderer:** https://www.nuget.org/packages/Svg.Controls.Avalonia/12.0.0.17. Приложение использует `Svg.Controls.Avalonia` 12.0.0.17 и XAML namespace `using:Avalonia.Svg`. Это не `Svg.Controls.Skia.Avalonia`: последний был удалён из-за конфликта major-версии SkiaSharp с выбранной Avalonia runtime. Иконки и логотип локальные (`PreOrderStore/Assets/Icons/`); изображения товаров приходят из DummyJSON и отображаются через `RemoteImage`.
- **Переключение темы:** палитра окна определена в `Window.Resources` как DynamicResource. При смене темы `MainWindow` обновляет кисти палитры, не меняя доменное состояние или данные SQLite. Выбор действует только до закрытия текущего окна.
- **Password-masked TextBox:** https://docs.avaloniaui.net/controls/input/text-input/textbox. Актуальный Avalonia `TextBox` поддерживает `PasswordChar` и двухстороннюю MVVM binding для текстового свойства; отдельного `PasswordBox` в используемой сборке нет.

Перед релизом следует перепроверить package updates и совместимость с точными версиями Avalonia и .NET SDK.
