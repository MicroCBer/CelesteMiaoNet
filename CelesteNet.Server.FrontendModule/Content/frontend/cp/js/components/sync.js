//@ts-check
import { FrontendStatusPanel } from "../panels/status.js";
import { FrontendCMDPanel } from "../panels/cmd.js";

export class FrontendSync {
  /**
   * @param {import("../frontend.js").Frontend} frontend
   */
  constructor(frontend) {
    this.frontend = frontend;

    /** @type {WebSocket} */
    this.ws = null;
    this.resync = this.resync.bind(this);
    this.onopen = this.onopen.bind(this);
    this.onclose = this.onclose.bind(this);
    this.onerror = this.onerror.bind(this);
    this.onmessage = this.onmessage.bind(this);

    this.state = "invalid";
    this.status = "init";

    /** @type {{resolve: (value?: any) => void; reject: (reason?: any) => void; info: string;}[]} */
    this.awaiting = [];

    this.logAllData = false;

    /** @type {Map<string, (data: any) => void>} */
    this.cmds = new Map();

    this.keepalive = setInterval(() => {
      this.run("echo", "keepalive");
    }, 6000);
  }

  /** @type {string} */
  get state() {
    return this._state;
  }
  set state(value) {
    this._state = value;

    /** @type {FrontendStatusPanel} */
    const sp = FrontendStatusPanel["instance"];
    if (sp)
      sp.render(null);
  }

  /** @type {string} */
  get status() {
    return this._status;
  }
  set status(value) {
    this._status = value;

    /** @type {FrontendStatusPanel} */
    const sp = FrontendStatusPanel["instance"];
    if (sp)
      sp.render(null);

    /** @type {FrontendCMDPanel} */
    const cmdp = FrontendCMDPanel["instance"];
    if (cmdp)
      cmdp.log(`// Status: ${value}`);
  }

  getFallbackCMD(id) {
    return data => console.log("sync nullcmd", id, data);
  }

  register(id, handler) {
    const existing = this.cmds.get(id);
    if (existing) {
      this.cmds.set(id, (...args) => {
        existing(...args);
        handler(...args);
      });
    } else {
      this.cmds.set(id, handler);
    }
  }

  run(cmd, data) {
    const stack = new Error().stack;
    return new Promise((resolve, reject) => {
      this.awaiting.push({
        resolve: resolve,
        reject: reject,
        info: stack
      });

      try {
        const ws = this.ws;
        ws.send("cmd");
        ws.send(cmd);
        ws.send(
          typeof(data) == "undefined" ? "" :
          JSON.stringify(data)
        );
      } catch (e) {
        reject(e);
      }
    });
  }

  resync() {
    if (!this.ws || this.ws.readyState === WebSocket.CLOSED) {
      this.frontend.snackbar({ text: "Connecting..." });
      this.status = "connecting";
      let ws = this.ws = new WebSocket(localStorage["frontend-ws"] || `${window.location.protocol === "https:" ? "wss" : "ws"}://${window.location.host}/api/ws`);
      if (!this.resyncPromiseResolve)
        this.resyncPromise = new Promise(resolve => this.resyncPromiseResolve = resolve);
      ws.onopen = this.onopen;
      ws.onclose = this.onclose;
      ws.onerror = this.onerror;
      ws.onmessage = this.onmessage;
    }

    if (this.resyncTimeout)
      clearTimeout(this.resyncTimeout);
    this.resyncTimeout = setTimeout(this.resync, 500);

    return this.resyncPromise;
  }

  reauth() {
    this.run("reauth", this.frontend.auth.key).then(valid => {
      if (!valid) {
        return this.frontend.auth.reauth().then(() => this.reauth());
      }
    }).then(() => {
      if (this.resyncPromiseResolve)
        this.resyncPromiseResolve();
      this.resyncPromiseResolve = null;
    });
  }

  close(reason) {
    this.status = "closing";
    this.state = "invalid";
    this.ws.close(1000, reason);
    this.resync();
  }

  /**
   * @param {Event} e
   */
  onopen(e) {
    this.frontend.snackbar({ text: "Connected." });
    this.status = "open";
    console.log("sync open", e);
    this.state = "waitForType";

    this.reauth();
  }

  /**
   * @param {CloseEvent} e
   */
  onclose(e) {
    this.frontend.snackbar({ text: e.reason ? `Connection closed: ${e.reason}` : "Connection closed." });
    this.status = e.reason ? `closed: ${e.reason}` : "closed";
    console.log("sync closed", e);

    for (let p of this.awaiting) {
      console.log("sync dead", p.info);
      p.reject(new Error("Connection closed."));
    }
    this.awaiting = [];

    /** @type {FrontendStatusPanel} */
    const sp = FrontendStatusPanel["instance"];
    if (sp)
      sp.refresh();
  }

  /**
   * @param {Event} e
   */
  onerror(e) {
    this.status = "error";
    console.log("sync error", e);
  }

  /**
   * @param {MessageEvent} e
   */
  onmessage(e) {
      /** @type {FrontendCMDPanel} */
      const cmdp = FrontendCMDPanel["instance"];

      let data;
      console.log("sync msg", e);

      switch (this.state) {
        case "waitForType":
          switch (e.data) {
            case "cmd":
              this.state = "waitForCMDID";
              break;

            case "data":
              this.state = "waitForData";
              break;

            default:
              this.close("unknown type");
              break;
          }
          break;


        case "waitForCMDID":
          console.log("sync cmd", e.data);
          this.currentCMD = this.cmds.get(e.data) || this.getFallbackCMD(e.data);
          this.state = "waitForCMDPayload";
          break;


        case "waitForCMDPayload":
          try {
            data = JSON.parse(e.data);
          } catch (e) {
            this.close("error on cmd data parse");
            break;
          }
          console.log("sync payload", data);

          try {
            this.currentCMD(data);
          } catch (e) {
            this.close("error on cmd run");
            break;
          }
          this.state = "waitForType";
          break;


        case "waitForData":
          try {
            data = JSON.parse(e.data);
          } catch (e) {
            this.close("error on data parse");
            break;
          }
          console.log("sync data", data);

          const a = this.awaiting.splice(0, 1)[0];
          if (a) {
            if (this.logAllData && cmdp) {
              cmdp.log(data);
            }
            a.resolve(data);
          } else if (cmdp) {
            cmdp.log(data);
          }

          this.state = "waitForType";
          break;


        default:
          this.close("unknown state");
          break;
      }
  }

}
