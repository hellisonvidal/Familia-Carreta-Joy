package com.familia.carretajoy;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageInstaller;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Atualizador da Carreta Joy 3D: instala o APK novo pela API oficial do Android (PackageInstaller).
 * Com a mesma assinatura e versão maior, não dá "app não instalado". No Android 12+, quando o
 * próprio jogo é quem instalou a versão atual, a atualização seguinte nem pede confirmação.
 */
public class CarretaUpdater {
    public static volatile String status = "";
    private static BroadcastReceiver receiver;

    public static boolean canInstall(Activity a) {
        if (Build.VERSION.SDK_INT >= 26) return a.getPackageManager().canRequestPackageInstalls();
        return true;
    }

    public static void openInstallPermission(Activity a) {
        try {
            Intent i = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + a.getPackageName()));
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            a.startActivity(i);
        } catch (Exception e) {
            status = "erro: " + e.getMessage();
        }
    }

    public static String getStatus() { return status; }

    public static void install(final Activity a, final String path) {
        status = "preparando";
        new Thread(new Runnable() {
            public void run() {
                try {
                    final String action = a.getPackageName() + ".INSTALL_STATUS";
                    synchronized (CarretaUpdater.class) {
                        if (receiver == null) {
                            receiver = new BroadcastReceiver() {
                                @Override
                                public void onReceive(Context c, Intent it) {
                                    int st = it.getIntExtra(PackageInstaller.EXTRA_STATUS, -999);
                                    if (st == PackageInstaller.STATUS_PENDING_USER_ACTION) {
                                        Intent confirm = it.getParcelableExtra(Intent.EXTRA_INTENT);
                                        if (confirm != null) {
                                            confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                            a.startActivity(confirm);
                                            status = "confirmar";
                                        }
                                    } else if (st == PackageInstaller.STATUS_SUCCESS) {
                                        status = "ok";
                                    } else {
                                        String msg = it.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);
                                        status = "erro: " + st + " " + (msg == null ? "" : msg);
                                    }
                                }
                            };
                            IntentFilter f = new IntentFilter(action);
                            if (Build.VERSION.SDK_INT >= 33) a.registerReceiver(receiver, f, Context.RECEIVER_EXPORTED);
                            else a.registerReceiver(receiver, f);
                        }
                    }
                    PackageInstaller pi = a.getPackageManager().getPackageInstaller();
                    PackageInstaller.SessionParams p = new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
                    p.setAppPackageName(a.getPackageName());
                    if (Build.VERSION.SDK_INT >= 31) p.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED);
                    int id = pi.createSession(p);
                    PackageInstaller.Session s = pi.openSession(id);
                    File file = new File(path);
                    InputStream in = new FileInputStream(file);
                    OutputStream out = s.openWrite("base.apk", 0, file.length());
                    byte[] buf = new byte[1 << 16];
                    int n;
                    while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                    s.fsync(out);
                    out.close();
                    in.close();
                    Intent it = new Intent(action).setPackage(a.getPackageName());
                    int flags = PendingIntent.FLAG_UPDATE_CURRENT;
                    if (Build.VERSION.SDK_INT >= 31) flags |= PendingIntent.FLAG_MUTABLE;
                    PendingIntent pend = PendingIntent.getBroadcast(a, id, it, flags);
                    s.commit(pend.getIntentSender());
                    s.close();
                    status = "instalando";
                } catch (Exception e) {
                    status = "erro: " + e.getMessage();
                }
            }
        }).start();
    }
}
