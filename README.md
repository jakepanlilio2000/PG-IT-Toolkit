# Puregold IT Toolkit 1.1.1

Release builds place the app, installer, and portable ZIP in `PuregoldITToolkit/Production`. Generated releases stay on disk and are excluded from Git.

## Changes

- **Local** reads receipts from the live/CONSO server. **FTP** reads your store’s uploaded receipts. **FTP Others** reads another store’s uploaded receipts and detects the store name.
- FTP login uses the existing account list automatically. Login fields have been removed from EJ and FTP settings. PurePOS commands use the saved connection details.
- INF remains inside **Forms**. The duplicate shortcut and added `.INF` file export have been removed. Extra item columns remain available in the email.
- Labels and help use simpler wording. The “PROD RELEASE” badge and misleading support contact have been removed. Loading old settings also removes that retired mailing-list address from INF recipients.
- Receipt finder accepts **123** for **7220010000000123**, as well as full receipt numbers. It searches receipt-number fields, rather than matching products or amounts. Other receipt filters are ignored in finder mode.
- The installer includes the app configuration and required WinSCP files. It includes .NET Framework 4.8 for computers that need it. Store settings and records are preserved on upgrades.

OB still supports high-quality images, dragging, arrow-key placement, and proportional resizing. EJ still preserves earlier outputs and shows receipt previews. Temporary cleanup preserves saved store records.

## Release files

- App: `PuregoldITToolkit/Production/Puregold IT Toolkit/PuregoldITToolkit.exe`
- Installer: `PuregoldITToolkit/Production/Puregold IT Toolkit/installer/PITK_Setup_v1.1.1.exe`
- Portable ZIP: `PuregoldITToolkit/Production/Puregold_IT_Toolkit_v1.1.1.zip`
- File hashes: `PuregoldITToolkit/Production/release.json`

Keep the app’s configuration, license, and WinSCP files beside it. Use the installer or extract the complete portable ZIP. Set the store and server address before using it at a different store.

The app uses `/opt/purepos/ejreceipt/done/MMddyyyy_EJReceiptJournal.zip` or `/opt/purepos/ejreceipt/MMddyyyy/` on CONSO. FTP uses `/PUREPOS_EJRECEIPT/`, store folders such as `(722) STORE NAME`, and `MMddyyyy_EJReceiptJournal.zip` archives.

## Build and checks

The project uses WPF, .NET Framework 4.8, and x86. Visual Studio needs the .NET desktop development workload and the .NET Framework 4.8 targeting pack. Restore the existing NuGet packages before the first build. Packaging also needs Inno Setup 6.

```powershell
./Verification/run.ps1 -Configuration Release
./Build-Production.ps1
```

`Build-Production.ps1` verifies the Release build, copies the app to Production, builds the installer, and creates the portable ZIP and file hashes. Fresh install defaults are separate from the current portable store configuration. The installer preserves existing settings, store records, and recipient choices.

Before packaging from a fresh checkout, place the Microsoft .NET Framework 4.8 offline installer, `NDP48-x86-x64-AllOS-ENU.exe`, in `PuregoldITToolkit/Production/Puregold IT Toolkit/`. This prerequisite and the generated packages are excluded from Git. The installer script, icon, and store CSV remain tracked. Local settings, receipt downloads, and app caches are also excluded.

Checks cover receipt matching, source selection, automatic FTP account retry, local file/ZIP processing, image placement, custom email columns, cleanup, and screen bindings. They use sample files, simulated CONSO/FTP sources, and a local FTP test server. Test files and screen renders stay in `Verification/bin`.

Live store connections, database updates, email delivery, and installation on older Windows versions still need acceptance testing in the intended environment. No live store records or services are changed by verification.
