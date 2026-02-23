document.addEventListener("DOMContentLoaded", function () {
  // Sidebar y modo oscuro
  const sidebarToggleBtn = document.getElementById("sidebarToggleBtn");
  const toggleIcon = document.getElementById("toggleIcon");
  const sidebar = document.getElementById("sidebar");
  const mainContent = document.getElementById("mainContent");
  const mobileMenuBtn = document.getElementById("mobileMenuBtn");
  const mobileOverlay = document.getElementById("mobileOverlay");
  const menuIcon = document.getElementById("menuIcon");
  const darkModeToggle = document.getElementById("darkModeToggle");
  const modeIcon = document.getElementById("modeIcon");
  const modeText = document.getElementById("modeText");
  const body = document.body;

  let sidebarOpen = false;
  let sidebarCollapsed = false;
  let darkMode = false;

  function toggleSidebarCollapse() {
    sidebarCollapsed = !sidebarCollapsed;

    if (sidebarCollapsed) {
      sidebar.classList.add("collapsed");
      mainContent.classList.add("sidebar-collapsed");
    } else {
      sidebar.classList.remove("collapsed");
      mainContent.classList.remove("sidebar-collapsed");
    }

    localStorage.setItem("sidebarCollapsed", sidebarCollapsed);
  }

  function toggleSidebar() {
    sidebarOpen = !sidebarOpen;

    if (sidebarOpen) {
      sidebar.classList.add("open");
      mobileOverlay.classList.add("active");
      menuIcon.className = "bx bx-x";
    } else {
      sidebar.classList.remove("open");
      mobileOverlay.classList.remove("active");
      menuIcon.className = "bx bx-menu";
    }
  }

  function toggleDarkMode() {
    darkMode = !darkMode;

    if (darkMode) {
      body.classList.add("dark");
      modeIcon.className = "bx bx-sun nav-icon";
      modeText.textContent = "Modo Claro";
    } else {
      body.classList.remove("dark");
      modeIcon.className = "bx bx-moon nav-icon";
      modeText.textContent = "Modo Oscuro";
    }

    localStorage.setItem("darkMode", darkMode);
  }

  if (sidebarToggleBtn)
    sidebarToggleBtn.addEventListener("click", toggleSidebarCollapse);
  if (mobileMenuBtn) mobileMenuBtn.addEventListener("click", toggleSidebar);
  if (mobileOverlay) mobileOverlay.addEventListener("click", toggleSidebar);
  if (darkModeToggle) darkModeToggle.addEventListener("click", toggleDarkMode);
  const logo = document.querySelector(".logo");
  if (logo) logo.addEventListener("dblclick", toggleSidebarCollapse);

  window.addEventListener("resize", function () {
    if (window.innerWidth >= 1024) {
      sidebar.classList.remove("open");
      mobileOverlay.classList.remove("active");
      menuIcon.className = "bx bx-menu";
      sidebarOpen = false;
    } else {
      sidebar.classList.remove("collapsed");
      mainContent.classList.remove("sidebar-collapsed");
      sidebarCollapsed = false;
      toggleIcon.className = "bx bx-chevron-left";
    }
  });

  window.addEventListener("load", function () {
    const savedDarkMode = localStorage.getItem("darkMode") === "true";
    if (savedDarkMode) {
      toggleDarkMode();
    }

    if (window.innerWidth >= 1024) {
      const savedSidebarCollapsed =
        localStorage.getItem("sidebarCollapsed") === "true";
      if (savedSidebarCollapsed) {
        toggleSidebarCollapse();
      }
    }
  });

  // MODAL DE CORREOS (igual que tu código)
  window.abrirModalCorreoPorFecha = function (tipo) {
    const fechaInput = document.getElementById("fechaBusqueda");
    const fecha = fechaInput ? fechaInput.value : "";

    if (!fecha) {
      alert("Debes seleccionar una fecha antes de enviar este correo.");
      return;
    }

    document.getElementById("formCorreoPreview").reset();
    document.getElementById("modalAdjuntos").innerHTML = "";
    document.getElementById("modalContenido").innerHTML = "";

    let url = `?handler=CorreoPreview&tipo=${tipo}&fecha=${encodeURIComponent(
      fecha
    )}`;

    fetch(url)
      .then((res) => res.json())
      .then((data) => {
        document.getElementById("modalAsunto").value = data.asunto || "";
        document.getElementById("modalDestinatarios").value =
          data.destinatarios || "";
        document.getElementById("modalCC").value = data.cc || "";
        document.getElementById("modalCCO").value = data.cco || "";
        document.getElementById("modalContenido").innerHTML =
          data.contenido || "";

        const adjuntosDiv = document.getElementById("modalAdjuntos");
        adjuntosDiv.innerHTML = "";
        if (data.adjuntos && Array.isArray(data.adjuntos)) {
          data.adjuntos.forEach(function (adj) {
            const link = document.createElement("a");
            link.textContent = adj;
            link.href = "/ruta/a/adjuntos/" + adj; // Cambia esto por la ruta real de tus archivos
            link.target = "_blank";
            link.className = "adjunto-item";
            adjuntosDiv.appendChild(link);
            adjuntosDiv.appendChild(document.createElement("br"));
          });
        }
      });

    document.getElementById("modal-correo-unico").style.display = "block";
    document.getElementById("modalOverlay").style.display = "block";
    document.body.style.overflow = "hidden";
  };
  window.abrirModalCorreoPorSeleccion = function (tipo) {
    // Obtiene los RUTs seleccionados por checkbox
    let seleccionados = Array.from(
      document.querySelectorAll('input[name="selectedEmpleados"]:checked')
    ).map((cb) => cb.value);

    // Si no hay seleccionados, intenta obtener el RUT del único empleado mostrado (búsqueda general)
    if (seleccionados.length === 0) {
      // Busca todos los empleados visibles en la tabla
      const visibles = Array.from(
        document.querySelectorAll('input[name="selectedEmpleados"]')
      );
      if (visibles.length === 1) {
        seleccionados = [visibles[0].value];
      }
    }

    // Si no hay empleados visibles o seleccionados, no abrir el modal
    if (seleccionados.length === 0) {
      alert(
        "Debes buscar y/o seleccionar al menos un empleado para enviar este correo."
      );
      return;
    }

    document.getElementById("formCorreoPreview").reset();
    document.getElementById("modalAdjuntos").innerHTML = "";
    document.getElementById("modalContenido").innerHTML = "";

    let url = `?handler=CorreoPreview&tipo=${tipo}&rut=${encodeURIComponent(
      seleccionados.join(",")
    )}`;

    fetch(url)
      .then((res) => res.json())
      .then((data) => {
        // Mostrar asunto, destinatarios, CC, CCO
        document.getElementById("modalAsunto").value = data.asunto || "";
        document.getElementById("modalDestinatarios").value =
          data.destinatarios || "";
        document.getElementById("modalCC").value = data.cc || "";
        document.getElementById("modalCCO").value = data.cco || "";

        // Mostrar cuerpo HTML
        document.getElementById("modalContenido").innerHTML =
          data.contenido || "";

        // Mostrar adjuntos
        const adjuntosDiv = document.getElementById("modalAdjuntos");
        adjuntosDiv.innerHTML = "";
        if (data.adjuntos && Array.isArray(data.adjuntos)) {
          data.adjuntos.forEach(function (adj) {
            const link = document.createElement("a");
            link.textContent = adj;
            link.href = "/ruta/a/adjuntos/" + adj; // Cambia esto por la ruta real de tus archivos
            link.target = "_blank";
            link.className = "adjunto-item";
            adjuntosDiv.appendChild(link);
            adjuntosDiv.appendChild(document.createElement("br"));
          });
        }
      });

    document.getElementById("modal-correo-unico").style.display = "block";
    document.getElementById("modalOverlay").style.display = "block";
    document.body.style.overflow = "hidden";
  };

  // Abrir modal de edición y cargar datos
  window.abrirModalEditarEmpleado = function (rut) {
    // Limpia los campos del modal
    document.getElementById("formEditarEmpleado").reset();

    // Oculta los grupos de selects "Otro"
    document.getElementById("analistaSelectGroup").style.display = "none";
    document.getElementById("analistaDisplayGroup").style.display = "block";
    document.getElementById("gerenciaSelectGroup").style.display = "none";
    document.getElementById("gerenciaDisplayGroup").style.display = "block";
    document.getElementById("ubicacionSelectGroup").style.display = "none";
    document.getElementById("ubicacionDisplayGroup").style.display = "block";

    // Carga los datos del empleado por AJAX
    fetch(
      "/PreInducciones/DatosyCorreos/DatosyCorreos?handler=GetEmpleado&rut=" +
        rut
    )
      .then((res) => res.json())
      .then((data) => {
        // Llena los campos del modal con los datos recibidos
        document.getElementById("editRut").value = data.RUT || "";
        document.getElementById("editNombre").value = data.Nombre || "";
        document.getElementById("editApellidoPaterno").value =
          data.Apellido_Paterno || "";
        document.getElementById("editApellidoMaterno").value =
          data.Apellido_Materno || "";
        document.getElementById("editFechaNacimiento").value =
          data.Fecha_Nacimiento || "";
        document.getElementById("editCorreo").value = data.Correo || "";
        document.getElementById("editTelefono").value = data.Telefono || "";
        document.getElementById("editDireccion").value = data.Direccion || "";
        document.getElementById("editSociedad").value = data.Sociedad || "";
        document.getElementById("editCargo").value = data.Cargo || "";
        document.getElementById("editGerencia").value = data.Gerencia || "";
        document.getElementById("editGerenciaMail").value =
          data.Gerencia_mail || "";
        document.getElementById("editUbicacion").value = data.Ubicacion || "";
        document.getElementById("editJefeDirecto").value =
          data.Jefe_Directo || "";
        document.getElementById("editTipoContrato").value =
          data.Tipo_de_Contrato || "";
        document.getElementById("editContrato").value = data.Contrato || "";
        document.getElementById("editTicket").value = data.Ticket || "";
        document.getElementById("editCarrera").value = data.Carrera || "";
        document.getElementById("editUniversidad").value =
          data.Universidad || "";
        document.getElementById("editFechaInduccion").value =
          data.Fecha_Induccion || "";
        document.getElementById("editFechaIngreso").value =
          data.Fecha_Ingreso || "";

        // Analista
        document.getElementById("analistaDisplay").value = data.Analista || "";
        document.getElementById("editAnalista").value = data.Analista || "";

        // Gerencia
        document.getElementById("gerenciaDisplay").value = data.Gerencia || "";
        document.getElementById("editGerencia").value = data.Gerencia || "";

        // Ubicación
        document.getElementById("ubicacionDisplay").value =
          data.Ubicacion || "";
        document.getElementById("editUbicacion").value = data.Ubicacion || "";
      });

    // Muestra el modal
    document.getElementById("modal-editar-empleado").style.display = "block";
    document.body.style.overflow = "hidden";
  };

  // Analista
  const btnEditarAnalista = document.getElementById("btnEditarAnalista");
  if (btnEditarAnalista) {
    btnEditarAnalista.addEventListener("click", function () {
      document.getElementById("analistaDisplayGroup").style.display = "none";
      document.getElementById("analistaSelectGroup").style.display = "block";
      setSelectOrOtro(
        "editAnalista",
        "editAnalistaOtro",
        document.getElementById("analistaDisplay").value
      );
    });
  }
  const editAnalista = document.getElementById("editAnalista");
  if (editAnalista) {
    editAnalista.addEventListener("change", function () {
      const otro = document.getElementById("editAnalistaOtro");
      if (this.value === "Otro") {
        otro.style.display = "block";
        otro.required = true;
        this.required = false;
      } else {
        otro.style.display = "none";
        otro.required = false;
        otro.value = "";
        this.required = true;
      }
    });
  }
  const btnCancelarAnalista = document.getElementById("btnCancelarAnalista");
  if (btnCancelarAnalista) {
    btnCancelarAnalista.addEventListener("click", function () {
      document.getElementById("analistaDisplayGroup").style.display = "block";
      document.getElementById("analistaSelectGroup").style.display = "none";
    });
  }

  // Gerencia
  const btnEditarGerencia = document.getElementById("btnEditarGerencia");
  if (btnEditarGerencia) {
    btnEditarGerencia.addEventListener("click", function () {
      document.getElementById("gerenciaDisplayGroup").style.display = "none";
      document.getElementById("gerenciaSelectGroup").style.display = "block";
      setSelectOrOtro(
        "editGerencia",
        "editGerenciaOtro",
        document.getElementById("gerenciaDisplay").value
      );
    });
  }
  const editGerencia = document.getElementById("editGerencia");
  if (editGerencia) {
    editGerencia.addEventListener("change", function () {
      const otro = document.getElementById("editGerenciaOtro");
      if (this.value === "Otro") {
        otro.style.display = "block";
        otro.required = true;
        this.required = false;
      } else {
        otro.style.display = "none";
        otro.required = false;
        otro.value = "";
        this.required = true;
      }
    });
  }
  const btnCancelarGerencia = document.getElementById("btnCancelarGerencia");
  if (btnCancelarGerencia) {
    btnCancelarGerencia.addEventListener("click", function () {
      document.getElementById("gerenciaDisplayGroup").style.display = "block";
      document.getElementById("gerenciaSelectGroup").style.display = "none";
    });
  }

  // Ubicación
  const btnEditarUbicacion = document.getElementById("btnEditarUbicacion");
  if (btnEditarUbicacion) {
    btnEditarUbicacion.addEventListener("click", function () {
      document.getElementById("ubicacionDisplayGroup").style.display = "none";
      document.getElementById("ubicacionSelectGroup").style.display = "block";
      setSelectOrOtro(
        "editUbicacion",
        "editUbicacionOtro",
        document.getElementById("ubicacionDisplay").value
      );
    });
  }
  const editUbicacion = document.getElementById("editUbicacion");
  if (editUbicacion) {
    editUbicacion.addEventListener("change", function () {
      const otro = document.getElementById("editUbicacionOtro");
      if (this.value === "Otro") {
        otro.style.display = "block";
        otro.required = true;
        this.required = false;
      } else {
        otro.style.display = "none";
        otro.required = false;
        otro.value = "";
        this.required = true;
      }
    });
  }
  const btnCancelarUbicacion = document.getElementById("btnCancelarUbicacion");
  if (btnCancelarUbicacion) {
    btnCancelarUbicacion.addEventListener("click", function () {
      document.getElementById("ubicacionDisplayGroup").style.display = "block";
      document.getElementById("ubicacionSelectGroup").style.display = "none";
    });
  }

  // Submit tradicional: lógica "Otro"
  const formEditarEmpleado = document.getElementById("formEditarEmpleado");
  if (formEditarEmpleado) {
    formEditarEmpleado.addEventListener("submit", function () {
      // Analista
      if (editAnalista && editAnalista.value === "Otro") {
        const analistaOtro = document.getElementById("editAnalistaOtro");
        if (analistaOtro && analistaOtro.value.trim() !== "") {
          editAnalista.value = analistaOtro.value.trim();
        }
      }
      // Gerencia
      if (editGerencia && editGerencia.value === "Otro") {
        const gerenciaOtro = document.getElementById("editGerenciaOtro");
        if (gerenciaOtro && gerenciaOtro.value.trim() !== "") {
          editGerencia.value = gerenciaOtro.value.trim();
        }
      }
      // Ubicación
      if (editUbicacion && editUbicacion.value === "Otro") {
        const ubicacionOtro = document.getElementById("editUbicacionOtro");
        if (ubicacionOtro && ubicacionOtro.value.trim() !== "") {
          editUbicacion.value = ubicacionOtro.value.trim();
        }
      }
    });
  }

  // Solicitud de cambios (usuario común)
  const btnSolicitarCambios = document.getElementById("btnSolicitarCambios");
  if (btnSolicitarCambios) {
    btnSolicitarCambios.addEventListener("click", function () {
      // Lógica "Otro" igual que submit tradicional
      if (editAnalista && editAnalista.value === "Otro") {
        const analistaOtro = document.getElementById("editAnalistaOtro");
        if (analistaOtro && analistaOtro.value.trim() !== "") {
          editAnalista.value = analistaOtro.value.trim();
        }
      }
      if (editGerencia && editGerencia.value === "Otro") {
        const gerenciaOtro = document.getElementById("editGerenciaOtro");
        if (gerenciaOtro && gerenciaOtro.value.trim() !== "") {
          editGerencia.value = gerenciaOtro.value.trim();
        }
      }
      if (editUbicacion && editUbicacion.value === "Otro") {
        const ubicacionOtro = document.getElementById("editUbicacionOtro");
        if (ubicacionOtro && ubicacionOtro.value.trim() !== "") {
          editUbicacion.value = ubicacionOtro.value.trim();
        }
      }

      // Construye el objeto con todos los datos del formulario
      const formData = new FormData(formEditarEmpleado);
      const datos = {};
      formData.forEach((value, key) => {
        datos[key] = value;
      });

      fetch(
        "/PreInducciones/DatosyCorreos/DatosyCorreos?handler=SolicitarEdicion",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(datos),
        }
      )
        .then((res) => res.json())
        .then((data) => {
          if (data.success) {
            alert(
              "Solicitud enviada correctamente. El administrador revisará tu solicitud."
            );
            cerrarModalEditarEmpleado();
          } else {
            alert(
              "Error al enviar solicitud: " +
                (data.error || "Error desconocido")
            );
          }
        });
    });
  }

  // Modal eliminar empleado
  window.abrirModalEliminarEmpleado = function (rut) {
    rutEmpleadoAEliminar = rut;
    document.getElementById("modal-eliminar-empleado").style.display = "block";
    document.body.style.overflow = "hidden";
  };
  window.cerrarModalEliminarEmpleado = function () {
    rutEmpleadoAEliminar = null;
    document.getElementById("modal-eliminar-empleado").style.display = "none";
    document.body.style.overflow = "auto";
  };

  // Modal editar empleado
  window.cerrarModalEditarEmpleado = function () {
    document.getElementById("modal-editar-empleado").style.display = "none";
    document.body.style.overflow = "auto";
  };

  // Reset modales al cargar
  document.getElementById("modal-editar-empleado").style.display = "none";
  document.getElementById("modal-eliminar-empleado").style.display = "none";
  var overlay = document.getElementById("modalOverlay");
  if (overlay) overlay.style.display = "none";
});

function cerrarModalCorreo() {
  document.getElementById("modal-correo-unico").style.display = "none";
  document.getElementById("modalOverlay").style.display = "none";
  document.body.style.overflow = "auto";
}
