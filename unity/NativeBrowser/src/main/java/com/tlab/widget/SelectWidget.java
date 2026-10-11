package com.tlab.widget;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;

public class SelectWidget {
    public static class Type {
        /**
         * Display choices in a menu that dismisses as soon as an item is chosen.
         */
        public static final int MENU = 1;

        /**
         * Display choices in a list that allows a single selection.
         */
        public static final int SINGLE = 2;

        /**
         * Display choices in a list that allows multiple selections.
         */
        public static final int MULTIPLE = 3;

        protected Type() {
        }
    }

    public static class Init extends BaseWidget.Init {
        public static final String KEY_OPTIONS = "options";
        public static final String KEY_TYPE = "type";

        private ArrayList<JSONObject> mOptions;
        private int mType;

        public void set(int type, ArrayList<JSONObject> options) {
            mType = type;
            mOptions = options;
        }

        @Override
        public JSONObject toJSON() {
            try {
                JSONObject jo = super.toJSON();
                jo.put(KEY_OPTIONS, new JSONArray(mOptions));
                jo.put(KEY_TYPE, mType);
                return jo;
            } catch (JSONException e) {
                throw new RuntimeException(e);
            }
        }
    }
}
