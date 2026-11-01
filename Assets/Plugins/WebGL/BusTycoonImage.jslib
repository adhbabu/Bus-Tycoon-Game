// Görüntüleri (JPEG/PNG) tarayıcının kendi çözücüsüyle, oyun döngüsünü dondurmadan dokuya çevirir (Game/WebImage.cs).
// Unity'nin LoadImage'ı WebAssembly içinde, ana iş parçacığında çözer: 1024 px bir harita döşemesi 30–100 ms takılma
// demekti. Burada indirme ve çözme tarayıcıda arka planda yapılır (createImageBitmap / img.decode); hazır olunca tek
// bir texImage2D ile ekran kartına yüklenir, mipmap'i ekran kartı üretir. Doku WebGL'de oluşturulur ve Unity'ye
// Texture2D.CreateExternalTexture ile verilir; işi bitince BusTycoon_ImageRelease ile silinir.
//
// Unity dokularında ilk satır görüntünün altıdır: görüntü dikey çevrilerek yüklenir. createImageBitmap'in flipY
// seçeneğini destekleyen tarayıcıda (bir kez denenir) çözme sırasında, desteklemeyende img öğesi +
// UNPACK_FLIP_Y_WEBGL ile yükleme sırasında çevrilir.
mergeInto(LibraryManager.library, {
  $BusTycoonImages: {
    next: 1,
    items: {},
    flipTest: null,

    flipSupported: function () {
      if (BusTycoonImages.flipTest) return BusTycoonImages.flipTest;
      BusTycoonImages.flipTest = new Promise(function (resolve) {
        try {
          if (typeof createImageBitmap !== "function") return resolve(false);
          var c = document.createElement("canvas");
          c.width = 1; c.height = 2;
          var x = c.getContext("2d");
          x.fillStyle = "#ff0000"; x.fillRect(0, 0, 1, 1);
          x.fillStyle = "#0000ff"; x.fillRect(0, 1, 1, 1);
          createImageBitmap(c, { imageOrientation: "flipY" }).then(function (bmp) {
            var d = document.createElement("canvas");
            d.width = 1; d.height = 2;
            var y = d.getContext("2d");
            y.drawImage(bmp, 0, 0);
            var top = y.getImageData(0, 0, 1, 1).data;
            if (bmp.close) bmp.close();
            resolve(top[2] > 128 && top[0] < 128); // üstte mavi: çevrilmiş
          }).catch(function () { resolve(false); });
        } catch (e) { resolve(false); }
      });
      return BusTycoonImages.flipTest;
    },

    decode: function (blob) {
      return BusTycoonImages.flipSupported().then(function (flip) {
        if (flip) {
          return createImageBitmap(blob, { imageOrientation: "flipY", premultiplyAlpha: "none", colorSpaceConversion: "none" })
            .then(function (bmp) { return { source: bmp, flipped: true, width: bmp.width, height: bmp.height }; });
        }
        return new Promise(function (resolve, reject) {
          var url = URL.createObjectURL(blob);
          var img = new Image();
          var done = function () {
            URL.revokeObjectURL(url);
            resolve({ source: img, flipped: false, width: img.naturalWidth, height: img.naturalHeight });
          };
          img.onerror = function () { URL.revokeObjectURL(url); reject(new Error("görüntü çözülemedi")); };
          img.src = url;
          if (img.decode) img.decode().then(done).catch(function () { img.onload = done; });
          else img.onload = done;
        });
      });
    }
  },

  BusTycoon_ImageLoad__deps: ["$BusTycoonImages"],
  BusTycoon_ImageLoad: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = BusTycoonImages.next++;
    var item = BusTycoonImages.items[id] = { state: 0, width: 0, height: 0, source: null, flipped: false };
    fetch(url).then(function (response) {
      if (!response.ok) throw new Error("HTTP " + response.status);
      return response.blob();
    }).then(function (blob) {
      return BusTycoonImages.decode(blob);
    }).then(function (result) {
      if (!BusTycoonImages.items[id]) { if (result.source.close) result.source.close(); return; } // iptal edildi
      item.source = result.source;
      item.flipped = result.flipped;
      item.width = result.width;
      item.height = result.height;
      item.state = 1;
    }).catch(function () {
      item.state = -1;
    });
    return id;
  },

  // 0 bekliyor, 1 hazır, -1 başarısız (ya da bilinmeyen kimlik)
  BusTycoon_ImageState__deps: ["$BusTycoonImages"],
  BusTycoon_ImageState: function (id) {
    var item = BusTycoonImages.items[id];
    return item ? item.state : -1;
  },

  BusTycoon_ImageWidth__deps: ["$BusTycoonImages"],
  BusTycoon_ImageWidth: function (id) {
    var item = BusTycoonImages.items[id];
    return item ? item.width : 0;
  },

  BusTycoon_ImageHeight__deps: ["$BusTycoonImages"],
  BusTycoon_ImageHeight: function (id) {
    var item = BusTycoonImages.items[id];
    return item ? item.height : 0;
  },

  // Hazır görüntüden mipmap'li WebGL dokusu oluşturur; Unity'nin doku kimliğini (GL.textures sırası) döndürür, olmazsa 0.
  BusTycoon_ImageUpload__deps: ["$BusTycoonImages"],
  BusTycoon_ImageUpload: function (id, maxAniso) {
    var item = BusTycoonImages.items[id];
    delete BusTycoonImages.items[id];
    if (!item || item.state !== 1 || typeof GLctx === "undefined") return 0;
    var gl = GLctx;
    var texture = gl.createTexture();
    if (!texture) return 0;
    var name = GL.getNewId(GL.textures);
    texture.name = name;
    GL.textures[name] = texture;

    var previous = gl.getParameter(gl.TEXTURE_BINDING_2D);
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, !item.flipped); // ImageBitmap için yok sayılır (zaten çevrildi)
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    try {
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, item.source);
      gl.generateMipmap(gl.TEXTURE_2D);
    } catch (e) {
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
      gl.bindTexture(gl.TEXTURE_2D, previous);
      gl.deleteTexture(texture);
      GL.textures[name] = null;
      return 0;
    }
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    var ext = gl.getExtension("EXT_texture_filter_anisotropic");
    if (ext && maxAniso > 1)
      gl.texParameterf(gl.TEXTURE_2D, ext.TEXTURE_MAX_ANISOTROPY_EXT, Math.min(maxAniso, gl.getParameter(ext.MAX_TEXTURE_MAX_ANISOTROPY_EXT)));
    gl.bindTexture(gl.TEXTURE_2D, previous); // Unity'nin bağlı doku önbelleği bozulmasın
    if (item.source.close) item.source.close();
    return name;
  },

  // Bekleyen ya da hazır görüntüyü bırakır (artık gerekmiyor).
  BusTycoon_ImageForget__deps: ["$BusTycoonImages"],
  BusTycoon_ImageForget: function (id) {
    var item = BusTycoonImages.items[id];
    delete BusTycoonImages.items[id];
    if (item && item.source && item.source.close) item.source.close();
  },

  // BusTycoon_ImageUpload'ın oluşturduğu dokuyu siler.
  BusTycoon_ImageRelease: function (name) {
    if (typeof GLctx === "undefined" || !GL.textures[name]) return;
    GLctx.deleteTexture(GL.textures[name]);
    GL.textures[name] = null;
  }
});
